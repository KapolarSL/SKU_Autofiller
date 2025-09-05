using System;
using System.Collections.Generic;
using System.Linq;                    // FirstOrDefault, Count, ToList
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace CNDA
{
    [Transaction(TransactionMode.Manual)]
    public class ConduitSkuAutofillCommand : IExternalCommand
    {
        private const string PhaseName = "Electrical";
        private const string SkuParamName = "SKU";

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var doc = data.Application.ActiveUIDocument.Document;

            // 1) Target elements
            var targets = GetElectricalPhaseElements(doc);
            if (targets.Count == 0)
            {
                TaskDialog.Show("CNDA", $"No elements found in phase '{PhaseName}'.");
                return Result.Cancelled;
            }

            // 2) Scope boxes (SKU source)
            var scopes = GetScopeSkus(doc);
            if (scopes.Count == 0)
            {
                TaskDialog.Show("CNDA", "No Scope Boxes found. Nothing to map SKUs from.");
                return Result.Cancelled;
            }

            // 3) Category ids
            var bicConduit = new ElementId((long)(int)BuiltInCategory.OST_Conduit);
            var bicConduitFitting = new ElementId((long)(int)BuiltInCategory.OST_ConduitFitting);
            var bicElectricalFixture = new ElementId((long)(int)BuiltInCategory.OST_ElectricalFixtures);

            // counters
            int wConduits = 0, wFittings = 0, wFixtures = 0;
            int oConduits = 0, oFittings = 0, oFixtures = 0;

            using (var tx = new Transaction(doc, "CNDA • SKU Autofill from Scope Boxes"))
            {
                tx.Start();

                foreach (var e in targets)
                {
                    var p = GetElementMidpoint(e);
                    string sku = FindSkuForPoint(p, scopes); // null if outside

                    bool isConduit = e.Category != null && e.Category.Id == bicConduit;
                    bool isFitting = e.Category != null && e.Category.Id == bicConduitFitting;
                    bool isFixture = e.Category != null && e.Category.Id == bicElectricalFixture;

                    if (sku == null)
                    {
                        if (isConduit) oConduits++;
                        else if (isFitting) oFittings++;
                        else if (isFixture) oFixtures++;
                        continue;
                    }

                    var prm = e.LookupParameter(SkuParamName);
                    if (prm == null || prm.IsReadOnly) continue;

                    prm.Set(sku);

                    if (isConduit) wConduits++;
                    else if (isFitting) wFittings++;
                    else if (isFixture) wFixtures++;
                }

                tx.Commit();
            }

            int total = targets.Count;

            TaskDialog.Show("CNDA",
              $"Phase '{PhaseName}': {total} elements\n\n" +
              $"Written to '{SkuParamName}':\n" +
              $"  Conduits: {wConduits}\n" +
              $"  Conduit Fittings: {wFittings}\n" +
              $"  Electrical Fixtures: {wFixtures}\n\n" +
              $"Unwritten (outside any Scope Box):\n" +
              $"  Conduits: {oConduits}\n" +
              $"  Conduit Fittings: {oFittings}\n" +
              $"  Electrical Fixtures: {oFixtures}\n\n" +
              $"Script author redacted");

            return Result.Succeeded;
        }

        // ---------- helpers ----------

        private static IReadOnlyList<Element> GetElectricalPhaseElements(Document doc)
        {
            var phase = new FilteredElementCollector(doc)
                .OfClass(typeof(Phase))
                .Cast<Phase>()
                .FirstOrDefault(p => string.Equals(p.Name, PhaseName, StringComparison.OrdinalIgnoreCase));

            if (phase == null) return Array.Empty<Element>();

            var cats = new LogicalOrFilter(new List<ElementFilter>
            {
                new ElementCategoryFilter(BuiltInCategory.OST_Conduit),
                new ElementCategoryFilter(BuiltInCategory.OST_ConduitFitting),
                new ElementCategoryFilter(BuiltInCategory.OST_ElectricalFixtures)
            });

            var pvp = new ParameterValueProvider(new ElementId((long)BuiltInParameter.PHASE_CREATED));
            var rule = new FilterElementIdRule(pvp, new FilterNumericEquals(), phase.Id);
            var createdInPhase = new ElementParameterFilter(rule);

            return new FilteredElementCollector(doc)
                .WherePasses(cats)
                .WherePasses(createdInPhase)
                .WhereElementIsNotElementType()
                .ToElements()
                .Cast<Element>()
                .ToList();
        }

        private class ScopeSku
        {
            public string Name;
            public BoundingBoxXYZ Box;
        }

        // Collect scope boxes by Category.Name == "Scope Boxes"
        private static List<ScopeSku> GetScopeSkus(Document doc)
        {
            return new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements()
                .Where(e => e.Category != null
                         && e.Category.Name != null
                         && e.Category.Name.Equals("Scope Boxes", StringComparison.OrdinalIgnoreCase))
                .Select(e => new ScopeSku { Name = e.Name, Box = e.get_BoundingBox(null) })
                .Where(s => s.Box != null)
                .ToList();
        }

        private static string FindSkuForPoint(XYZ worldPoint, List<ScopeSku> scopes)
        {
            foreach (var s in scopes)
                if (PointInsideScopeBox(worldPoint, s.Box))
                    return s.Name;
            return null;
        }

        private static XYZ GetElementMidpoint(Element e)
        {
            if (e.Location is LocationPoint lp) return lp.Point;

            if (e.Location is LocationCurve lc && lc.Curve != null)
                return lc.Curve.Evaluate(0.5, true);

            var bb = e.get_BoundingBox(null);
            if (bb != null)
            {
                var minW = bb.Transform.OfPoint(bb.Min);
                var maxW = bb.Transform.OfPoint(bb.Max);
                return (minW + maxW) * 0.5;
            }
            return XYZ.Zero;
        }

        private static bool PointInsideScopeBox(XYZ worldPoint, BoundingBoxXYZ scopeBox)
        {
            var inv = scopeBox.Transform.Inverse;
            var p = inv.OfPoint(worldPoint);

            return p.X >= scopeBox.Min.X && p.X <= scopeBox.Max.X
                && p.Y >= scopeBox.Min.Y && p.Y <= scopeBox.Max.Y
                && p.Z >= scopeBox.Min.Z && p.Z <= scopeBox.Max.Z;
        }
    }
}
