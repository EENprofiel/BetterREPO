using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace TruckEnergyDisplay;

// Reads method metadata only. Never invokes a game method or emits replacement game code.
internal sealed class Il
{
    internal int Offset;
    internal OpCode Op;
    internal object? Value;
    internal bool Field(string type, string name) => Value is FieldInfo f && f.DeclaringType?.Name == type && f.Name == name;
    internal bool Method(string name) => Value is MethodBase m && m.Name == name;
    internal bool Text(string value) => Op == OpCodes.Ldstr && Equals(Value, value);
    internal int Local()
    {
        if (Op == OpCodes.Ldloc_0 || Op == OpCodes.Stloc_0) return 0;
        if (Op == OpCodes.Ldloc_1 || Op == OpCodes.Stloc_1) return 1;
        if (Op == OpCodes.Ldloc_2 || Op == OpCodes.Stloc_2) return 2;
        if (Op == OpCodes.Ldloc_3 || Op == OpCodes.Stloc_3) return 3;
        if (Op == OpCodes.Ldloc || Op == OpCodes.Ldloc_S || Op == OpCodes.Stloc || Op == OpCodes.Stloc_S)
            return Convert.ToInt32(Value);
        return -1;
    }
    internal bool StoreLocal => Op.Name!.StartsWith("stloc", StringComparison.Ordinal);
    internal bool LoadLocal => Op.Name!.StartsWith("ldloc", StringComparison.Ordinal) && !Op.Name.StartsWith("ldloca", StringComparison.Ordinal);
    internal bool Number(out double n, out bool floating)
    {
        floating = Op == OpCodes.Ldc_R4 || Op == OpCodes.Ldc_R8;
        if (floating || Op == OpCodes.Ldc_I4 || Op == OpCodes.Ldc_I4_S) { n = Convert.ToDouble(Value); return true; }
        if (Op == OpCodes.Ldc_I4_M1) { n = -1; return true; }
        if (Op.Value >= OpCodes.Ldc_I4_0.Value && Op.Value <= OpCodes.Ldc_I4_8.Value) {
            n = Op.Value - OpCodes.Ldc_I4_0.Value; return true;
        }
        n = 0; return false;
    }
}

internal static class IlReader
{
    private static readonly Dictionary<short, OpCode> Codes = BuildCodes();
    private static Dictionary<short, OpCode> BuildCodes()
    {
        var result = new Dictionary<short, OpCode>();
        foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (f.GetValue(null) is OpCode op) result[op.Value] = op;
        return result;
    }
    internal static List<Il> Read(MethodBase method)
    {
        byte[] bytes = method.GetMethodBody()?.GetILAsByteArray() ?? throw new InvalidOperationException("Method body is unavailable");
        var list = new List<Il>();
        int p = 0;
        var module = method.Module;
        while (p < bytes.Length) {
            int start = p;
            short code = bytes[p++];
            if (code == 0xfe) code = unchecked((short)(0xfe00 | bytes[p++]));
            OpCode op = Codes[code];
            object? value = null;
            switch (op.OperandType) {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineI: value = (sbyte)bytes[p++]; break;
                case OperandType.ShortInlineVar: value = (int)bytes[p++]; break;
                case OperandType.InlineVar: value = (int)BitConverter.ToUInt16(bytes, p); p += 2; break;
                case OperandType.InlineI: value = BitConverter.ToInt32(bytes, p); p += 4; break;
                case OperandType.InlineI8: value = BitConverter.ToInt64(bytes, p); p += 8; break;
                case OperandType.ShortInlineR: value = BitConverter.ToSingle(bytes, p); p += 4; break;
                case OperandType.InlineR: value = BitConverter.ToDouble(bytes, p); p += 8; break;
                case OperandType.ShortInlineBrTarget: value = p + 1 + (sbyte)bytes[p]; p++; break;
                case OperandType.InlineBrTarget: value = p + 4 + BitConverter.ToInt32(bytes, p); p += 4; break;
                case OperandType.InlineSwitch:
                    int count = BitConverter.ToInt32(bytes, p); p += 4 + count * 4; break;
                case OperandType.InlineString: value = module.ResolveString(BitConverter.ToInt32(bytes, p)); p += 4; break;
                case OperandType.InlineField: value = module.ResolveField(BitConverter.ToInt32(bytes, p)); p += 4; break;
                case OperandType.InlineMethod: value = module.ResolveMethod(BitConverter.ToInt32(bytes, p)); p += 4; break;
                case OperandType.InlineType: case OperandType.InlineTok: case OperandType.InlineSig:
                    value = BitConverter.ToInt32(bytes, p); p += 4; break;
                default: throw new InvalidOperationException("Unsupported IL operand");
            }
            if (op != OpCodes.Nop) list.Add(new Il { Offset = start, Op = op, Value = value });
        }
        return list;
    }
}

// A small arithmetic expression, not an IL emulator. Calls and side effects are prohibited.
internal sealed class CapExpression
{
    private readonly Func<Func<string, double>, double> _evaluate;
    internal bool Float { get; }
    private CapExpression(Func<Func<string, double>, double> evaluate, bool floating) { _evaluate = evaluate; Float = floating; }
    internal double Evaluate(Func<string, double> fields) => _evaluate(fields);
    internal static CapExpression Parse(List<Il> il, ref int p, int depth = 0)
    {
        if (p < 0 || depth > 32) throw new InvalidOperationException("Unrecognized capacity expression");
        Il i = il[p--];
        if (i.Number(out double n, out bool floating)) return new CapExpression(_ => n, floating);
        if (i.Op == OpCodes.Ldfld && i.Value is FieldInfo f && f.DeclaringType?.Name == "ChargingStation" &&
            (f.Name == "chargeInt" || f.Name == "maxCrystals" || f.Name == "energyPerCrystal") &&
            p >= 0 && il[p--].Op == OpCodes.Ldarg_0) {
            string name = f.Name;
            return new CapExpression(fields => fields(name), f.FieldType == typeof(float));
        }
        if (i.Op == OpCodes.Conv_R4 || i.Op == OpCodes.Conv_I4) {
            var a = Parse(il, ref p, depth + 1);
            return new CapExpression(fields => i.Op == OpCodes.Conv_R4 ? (double)(float)a.Evaluate(fields) : checked((int)a.Evaluate(fields)), i.Op == OpCodes.Conv_R4);
        }
        if (i.Method("RoundToInt") && i.Value is MethodBase round && round.DeclaringType?.FullName == "UnityEngine.Mathf") {
            var a = Parse(il, ref p, depth + 1);
            return new CapExpression(fields => checked((int)Math.Round((float)a.Evaluate(fields), MidpointRounding.ToEven)), false);
        }
        if (i.Op == OpCodes.Mul || i.Op == OpCodes.Div || i.Op == OpCodes.Add || i.Op == OpCodes.Sub) {
            var b = Parse(il, ref p, depth + 1);
            var a = Parse(il, ref p, depth + 1);
            bool fp = a.Float || b.Float;
            return new CapExpression(fields => {
                double x = a.Evaluate(fields), y = b.Evaluate(fields);
                if (i.Op == OpCodes.Div && y == 0) throw new DivideByZeroException();
                double v = i.Op == OpCodes.Mul ? x * y : i.Op == OpCodes.Div ? x / y : i.Op == OpCodes.Add ? x + y : x - y;
                return fp ? (double)(float)v : checked((int)v);
            }, fp);
        }
        throw new InvalidOperationException("Unsupported capacity instruction: " + i.Op.Name);
    }
}
