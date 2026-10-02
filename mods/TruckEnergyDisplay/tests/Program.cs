using System.Reflection;
using System.Runtime.Loader;
using TruckEnergyDisplay;

int checks = 0;
void Check(bool result, string name) { if (!result) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
void Near(double actual, double expected, string name) => Check(Math.Abs(actual - expected) < 0.000001, name);
Func<int, double> linear = n => n * 10;
var example = EnergyMath.Calculate(63, 100, 10, 7, 3, linear, true);
Check(example.Needed == 4 && example.StillNeeded == 1, "request example needs four, buying three leaves one");
Near(example.Projected, 93, "request example projects 93%");
var empty = EnergyMath.Calculate(0, 100, 17, 0, 6, linear, true);
Check(empty.Needed == 10, "ownership limit requires ten, not naive ceiling six");
Near(empty.Projected, 60, "checkout credit 100 still loads only 60 with six owned");
Check(empty.StillNeeded == 4, "four more reach full after loading");
var zero = EnergyMath.Calculate(0, 100, 10, 0, 0, linear, true);
Check(zero.Buying == 0 && zero.Needed == 10 && zero.Projected == 0, "zero stock and zero checkout");
var full = EnergyMath.Calculate(100, 100, 17, 10, 0, linear, true);
Check(full.Needed == 0 && full.StillNeeded == 0 && full.Projected == 100, "full station needs none");
var extra = EnergyMath.Calculate(90, 100, 17, 9, 4, linear, true);
Check(extra.Needed == 1 && extra.Extra == 3 && extra.Projected == 100, "overbuy four at 90 leaves three extras");
var fractional = EnergyMath.Calculate(62.5, 125, 12.5, 5, 4, n => n * 12.5, true);
Check(fractional.Needed == 5 && fractional.StillNeeded == 1, "fractional modified capacity");
Near(fractional.Projected, 112.5, "fractional projection");
var boundary = EnergyMath.Calculate(99.99999, 100, 10, 10, 0, linear, true);
Check(boundary.Needed == 1, "near-full is not falsely marked full");
var notAffordable = EnergyMath.Calculate(30, 100, 17, 3, 2, linear, false);
Check(!notAffordable.Affordable, "insufficient-funds qualifier retained");
var changedCart = EnergyMath.Calculate(63, 100, 17, 7, 0, linear, true);
Check(changedCart.Buying == 0 && changedCart.Projected == 63, "removed checkout crystals do not persist");
var purchased = EnergyMath.Calculate(97, 100, 17, 9, 0, linear, true);
Check(purchased.Current == 97 && purchased.Projected == 90, "saved checkout energy and next-truck ownership cap kept distinct");
bool threw = false;
try { EnergyMath.Required(100, _ => 0); } catch (InvalidOperationException) { threw = true; }
Check(threw, "unreachable full charge fails closed");
string nonce = new string('a', 32);
object[] packet = SnapshotProtocol.Encode(nonce, 3, example);
Check(SnapshotProtocol.Decode(packet, 7, 7, nonce, 3, out _), "valid host snapshot accepted");
Check(!SnapshotProtocol.Decode(packet, 8, 7, nonce, 3, out _), "non-host rejected");
Check(!SnapshotProtocol.Decode(packet, 7, 8, nonce, 3, out _), "old master rejected after migration");
Check(!SnapshotProtocol.Decode(packet, 7, 7, new string('b',32), 3, out _), "old scene nonce rejected");
Check(!SnapshotProtocol.Decode(packet, 7, 7, nonce, 4, out _), "old run stage rejected");
var corrupt = (object[])packet.Clone(); corrupt[5] = double.NaN;
Check(!SnapshotProtocol.Decode(corrupt, 7, 7, nonce, 3, out _), "NaN energy rejected");
corrupt = (object[])packet.Clone(); corrupt[8] = -1;
Check(!SnapshotProtocol.Decode(corrupt, 7, 7, nonce, 3, out _), "negative checkout rejected");

if (args.Length > 0) {
    string directory = Path.GetFullPath(args[0]);
    AssemblyLoadContext.Default.Resolving += (ctx, name) => {
        string path = Path.Combine(directory, name.Name + ".dll");
        return File.Exists(path) ? ctx.LoadFromAssemblyPath(path) : null;
    };
    var game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory, "Assembly-CSharp.dll"));
    var mechanics = MechanicsDiscovery.Discover(game.GetType("ChargingStation", true)!, game.GetType("ExtractionPoint", true)!);
    Near(mechanics.Increment, 17, "real assembly checkout increment is 17");
    Near(mechanics.Maximum, 100, "real assembly normalized cap is 100");
    Check(mechanics.OwnedItemKey == "Item Power Crystal", "ownership key extracted from game IL");
    double Cap(int owned, double per = 10, double max = 10) => mechanics.Cap.Evaluate(name => name switch {
        "chargeInt" => owned, "energyPerCrystal" => per, "maxCrystals" => max, _ => throw new Exception(name)
    });
    Near(Cap(6), 60, "real station Start expression yields 60 for six crystals");
    Near(Cap(3, 10, 6), 50, "integer division in game expression is preserved");
    Near(Cap(10), 100, "real station Start expression fills with ten");
    var actual = EnergyMath.Calculate(0, mechanics.Maximum, mechanics.Increment, 0, 6, n => Cap(n), true);
    Check(actual.Needed == 10 && actual.Projected == 60, "end-to-end discovery and projection from real assembly");
    Console.WriteLine("Assembly SHA256: " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(directory,"Assembly-CSharp.dll")))).ToLowerInvariant());
}
Console.WriteLine($"All {checks} checks passed.");
