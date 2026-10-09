using OwnedEquipmentHUD;

int checks = 0;
void Check(bool result, string name) { if (!result) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }

// Purchase limit
Check(PurchaseLimit.Resolve(false, 5) == 0, "maxPurchase false means no limit even with an amount");
Check(PurchaseLimit.Resolve(true, 3) == 3, "maxPurchase true uses the amount");
Check(PurchaseLimit.Resolve(null, 3) == 3, "missing maxPurchase flag still uses a positive amount");
Check(PurchaseLimit.Resolve(true, 0) == 0, "zero amount means no limit");
Check(PurchaseLimit.Resolve(true, -1) == 0, "negative amount means no limit");
Check(PurchaseLimit.Resolve(null, null) == 0, "no data means no limit");
Check(PurchaseLimit.FormatCount(2, 0) == "x2", "no limit shows only the count");
Check(PurchaseLimit.FormatCount(2, 3) == "x2/3", "limit shows owned/limit");
Check(PurchaseLimit.FormatOwned(2, 3) == "2/3" && PurchaseLimit.FormatOwned(2, 0) == "2", "owned text");
Check(PurchaseLimit.FormatAfterPurchase(2, 3) == "After purchase: 3", "below limit previews next purchase");
Check(PurchaseLimit.FormatAfterPurchase(3, 3) == "Limit reached", "at limit says limit reached");
Check(PurchaseLimit.FormatAfterPurchase(4, 3) == "Limit reached", "above limit says limit reached");
Check(PurchaseLimit.FormatAfterPurchase(9, 0) == "After purchase: 10", "no limit never says limit reached");

// Icon / text fallback
Check(HudLayout.ShowsName(HudDisplayMode.Text, true) && !HudLayout.ShowsIcon(HudDisplayMode.Text, true), "text mode never shows icons");
Check(HudLayout.ShowsIcon(HudDisplayMode.Icons, true) && !HudLayout.ShowsName(HudDisplayMode.Icons, true), "icons mode shows icon only");
Check(HudLayout.ShowsName(HudDisplayMode.Icons, false) && !HudLayout.ShowsIcon(HudDisplayMode.Icons, false), "icons mode falls back to text without an icon");
Check(HudLayout.ShowsIcon(HudDisplayMode.Both, true) && HudLayout.ShowsName(HudDisplayMode.Both, true), "both mode shows icon and name");
Check(HudLayout.ShowsName(HudDisplayMode.Both, false), "both mode falls back to text without an icon");

// Panel stays on screen for common resolutions and both modes
var screens = new (float w, float h, string n)[] { (1920, 1080, "1080p"), (2560, 1440, "1440p"), (3440, 1440, "ultrawide"), (5120, 1440, "super ultrawide"), (1280, 720, "720p"), (800, 600, "tiny") };
foreach (var s in screens)
foreach (var mode in Enum.GetValues<HudDisplayMode>())
foreach (var compact in new[] { false, true })
foreach (var entries in new[] { 0, 1, 12, 60 })
foreach (var context in new[] { false, true })
{
    var l = HudLayout.Compute(s.w, s.h, entries, 50, true, context, mode, compact);
    string label = $"{s.n} {mode} compact={compact} entries={entries} context={context}";
    Check(l.X >= 0 && l.Y >= 0 && l.X + l.Width <= s.w + 0.01f, "inside horizontally: " + label);
    // Fixed chrome plus one row must fit; below that the panel cannot shrink further.
    if (s.h >= 720) Check(l.Y + l.Height <= s.h + 0.01f, "inside vertically: " + label);
}

var many = HudLayout.Compute(1920, 1080, 60, 50, true, false, HudDisplayMode.Text, false);
Check(many.Scrollable && many.VisibleRows < 50, "long list on 1080p is limited and scrollable");
var few = HudLayout.Compute(1920, 1080, 3, 12, true, false, HudDisplayMode.Text, false);
Check(!few.Scrollable && few.VisibleRows == 3, "short list shows every row");
var capped = HudLayout.Compute(1920, 1080, 30, 5, true, false, HudDisplayMode.Text, false);
Check(capped.VisibleRows == 5 && capped.Scrollable, "MaxVisibleRows is respected");
var wide = HudLayout.Compute(3440, 1440, 3, 12, true, false, HudDisplayMode.Text, false);
Check(wide.X + wide.Width <= 3440 && wide.X > 3440 * 0.8f, "ultrawide keeps the panel at the right edge");
var cmp = HudLayout.Compute(1920, 1080, 3, 12, true, false, HudDisplayMode.Text, true);
Check(cmp.Width < few.Width && cmp.Height < few.Height, "compact is smaller");
var contextOnly = HudLayout.Compute(1920, 1080, 5, 12, false, true, HudDisplayMode.Text, false);
Check(contextOnly.VisibleRows == 0 && contextOnly.Height > 0, "context-only panel has no rows");

Console.WriteLine($"{checks} checks passed");
