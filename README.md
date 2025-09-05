# Revit SKU Autofill Add‑in (CNDA)

A native Revit add‑in (C#) that auto‑propagates **SKU** and **Run Name** style metadata across MEP elements to cut manual data entry and keep BOMs clean.

## What it does

* Collects target elements from a chosen project phase (Conduits, Conduit Fittings, Electrical Fixtures).
* Reads **Scope Boxes** and treats each box name as the SKU value to apply.
* For each target element, finds its midpoint and checks which scope box it belongs to.
* Writes the SKU into a configurable text parameter on the element, skipping read‑only or missing params.
* Summarizes how many elements were written vs skipped.

## Why it’s useful

* **Consistency**: one source of truth for SKU assignment using scope boxes.
* **Speed**: no more hand‑typing SKUs across runs.
* **Prefabrication‑ready**: standardized data for schedules/exports and downstream BOM.

## How it works (high level)

1. Locate a project **Phase** by name.
2. Collect Conduits, Conduit Fittings, and Electrical Fixtures created in that phase.
3. Collect all objects in the **Scope Boxes** category and cache their world‑space bounding boxes.
4. For each target element, compute a representative point (location point, curve midpoint, or bbox center).
5. If the point lies inside a scope box, use that box’s **Name** as the SKU value to write to the element’s parameter.
6. Wrap updates in a single Revit **Transaction** and report results.

## Configuration

The add‑in uses two small constants you can edit in code before building:

* **Phase name** used to filter elements (e.g., `"Electrical"`).
* **SKU parameter name** to write into (must exist as a non‑read‑only text parameter on the target categories).

> Tip: if your organization uses a different parameter naming convention, change the constant and rebuild. Keep it generic if you plan to open‑source.

## Requirements

* Revit version matching your build target (.NET Framework per that Revit release)
* Visual Studio (C#)
