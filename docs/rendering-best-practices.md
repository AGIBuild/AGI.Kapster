# Rendering Best Practices

## Canvas Layout and Rendering

### Critical: Canvas Measure and Arrange Before Rendering

**Issue**: When dynamically creating a `Canvas` for off-screen rendering (not in visual tree), elements positioned with `Canvas.SetLeft()` and `Canvas.SetTop()` may not be correctly placed unless the Canvas undergoes a layout pass.

**Symptom**: Annotations (especially ellipses and rectangles) appear offset or misaligned when exported to clipboard or file, even though they appear correct in the live overlay.

**Root Cause**: 
- `Canvas` requires layout calculation via `Measure()` and `Arrange()` to correctly position child elements
- Without these calls, `Canvas.SetLeft/SetTop` attached properties may not be applied correctly
- `RenderTargetBitmap.Render()` captures the Canvas state **as-is**, without triggering layout

**Solution**: Always call `Measure()` and `Arrange()` before rendering a dynamically created Canvas:

```csharp
// In ExportService.CreateCompositeImageWithAnnotationsAsync
var canvas = new Canvas
{
    Width = selectionRect.Width,   // DIPs
    Height = selectionRect.Height, // DIPs
    Background = Brushes.Transparent
};

// Add child elements to canvas
_renderer.RenderAll(canvas, offsetAnnotations);

// CRITICAL: Force layout pass before rendering
canvas.Measure(new Size(selectionRect.Width, selectionRect.Height));
canvas.Arrange(new Rect(0, 0, selectionRect.Width, selectionRect.Height));

// Now safe to render
var annotationBitmap = new RenderTargetBitmap(
    new PixelSize(pixelWidth, pixelHeight),
    new Vector(96 * scaleX, 96 * scaleY));
annotationBitmap.Render(canvas);
```

**Key Points**:
1. Call `Measure()` with the desired size
2. Call `Arrange()` with a Rect starting at (0, 0)
3. Both calls must complete **before** `RenderTargetBitmap.Render()`
4. This applies to any dynamically created `Canvas` not in the visual tree

**Related Files**:
- `src/AGI.Kapster.Desktop/Services/Export/ExportService.cs` - Implements the fix
- `src/AGI.Kapster.Desktop/Rendering/AnnotationRenderer.cs` - Uses `Canvas.SetLeft/SetTop` for positioning

---

## Annotation Coordinate Systems

### Overview
The application uses multiple coordinate systems that must be carefully transformed:

1. **Screen Coordinates**: Physical screen pixels, can span multiple monitors
2. **Window Coordinates (DIPs)**: Device-independent pixels, relative to overlay window
3. **Selection Coordinates**: Relative to the selected region within the window
4. **Export Coordinates**: Physical pixels in the exported image

### Coordinate Transformation Flow

```
┌─────────────────────────────────────────────────────────┐
│ 1. User draws annotation in overlay window             │
│    Coordinates: Window DIPs (e.g., 1000, 2000)         │
└─────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────┐
│ 2. User selects region to export                       │
│    Selection: (373, 1675, 830, 575) DIPs               │
└─────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────┐
│ 3. Offset annotations to selection-relative coords     │
│    Offset: (-373, -1675)                                │
│    New coords: (627, 325) - relative to selection       │
└─────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────┐
│ 4. Render to RenderTargetBitmap at physical resolution │
│    Scale: 1x1 (or 2x2 for Retina/HiDPI)                │
│    Canvas is measured/arranged before rendering         │
└─────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────┐
│ 5. Export to clipboard or file                          │
│    Result: Pixel-perfect match with overlay display     │
└─────────────────────────────────────────────────────────┘
```

### Key Implementation Details

**1. Arrow Annotations**: Use absolute coordinates in geometry
```csharp
// ArrowAnnotation stores StartPoint and EndPoint with absolute coordinates
// TacticalArrowBuilder creates geometry with those exact coordinates
// No Canvas.SetLeft/SetTop needed - geometry is self-positioning
```

**2. Shape Annotations (Rectangle, Ellipse)**: Use geometry + Canvas positioning
```csharp
// Geometry created at local origin (0, 0)
var geometry = new EllipseGeometry(new Rect(0, 0, width, height));

// Positioned via Canvas attached properties
Canvas.SetLeft(path, annotation.BoundingRect.X);
Canvas.SetTop(path, annotation.BoundingRect.Y);
```

**3. Offset Calculation**:
```csharp
// ExportService.CreateOffsetAnnotation
var offsetX = -selectionRect.X;
var offsetY = -selectionRect.Y;

// For shapes, offset the geometric bounds
new EllipseAnnotation(
    new Rect(
        ellipse.BoundingRect.X + offsetX,
        ellipse.BoundingRect.Y + offsetY,
        ellipse.BoundingRect.Width,
        ellipse.BoundingRect.Height),
    style);

// For arrows, offset StartPoint, EndPoint, and all Trail points
```

---

## DPI Scaling Considerations

### High-DPI Display Support

**Challenge**: On high-DPI displays (e.g., Retina), physical pixels ≠ DIPs.

**Solution**: Calculate scale factor and apply to RenderTargetBitmap:

```csharp
// Calculate scale from frozen background
var scaleX = _frozenBackground.PixelSize.Width / this.Bounds.Width;
var scaleY = _frozenBackground.PixelSize.Height / this.Bounds.Height;

// Create RenderTargetBitmap with scaled DPI
var bitmap = new RenderTargetBitmap(
    new PixelSize(physicalWidth, physicalHeight),
    new Vector(96 * scaleX, 96 * scaleY));  // DPI matches scale
```

**Key Points**:
1. Always extract scale from actual captured screenshot, not from Screen.Scaling
2. Apply scale to both dimensions independently (may differ)
3. Canvas size stays in DIPs; RenderTargetBitmap converts to physical pixels
4. DPI parameter to RenderTargetBitmap ensures correct scaling

---

## Common Pitfalls

### 1. Forgetting Canvas Layout Pass
❌ **Wrong**:
```csharp
var canvas = new Canvas { Width = 800, Height = 600 };
// Add children...
bitmap.Render(canvas); // Elements may be misaligned!
```

✅ **Correct**:
```csharp
var canvas = new Canvas { Width = 800, Height = 600 };
// Add children...
canvas.Measure(new Size(800, 600));
canvas.Arrange(new Rect(0, 0, 800, 600));
bitmap.Render(canvas); // Perfect alignment
```

### 2. Using Bounds Instead of Geometric Properties
❌ **Wrong**:
```csharp
// Bounds includes stroke width padding
new EllipseAnnotation(ellipse.Bounds, style);
```

✅ **Correct**:
```csharp
// Use actual geometric properties
new EllipseAnnotation(ellipse.BoundingRect, style);
```

### 3. Forgetting to Offset Arrow Trail Points
❌ **Wrong**:
```csharp
var offsetArrow = new ArrowAnnotation(
    new Point(arrow.StartPoint.X + offsetX, arrow.StartPoint.Y + offsetY),
    new Point(arrow.EndPoint.X + offsetX, arrow.EndPoint.Y + offsetY),
    style);
// Trail points not offset - arrow path will be wrong!
```

✅ **Correct**:
```csharp
var offsetArrow = new ArrowAnnotation(...);
offsetArrow.Trail = arrow.Trail
    .Select(p => new Point(p.X + offsetX, p.Y + offsetY))
    .ToList();
```

---

## Testing Checklist

When implementing new annotation types or modifying export logic:

- [ ] Test on standard DPI display (100% scaling)
- [ ] Test on high-DPI display (150%, 200% scaling)
- [ ] Test with annotations at various positions in selection
- [ ] Test with annotations partially outside selection (clipping)
- [ ] Compare live overlay rendering with exported image pixel-by-pixel
- [ ] Test both clipboard and file export
- [ ] Test with multiple annotation types together
- [ ] Verify arrow Trail points are correctly offset

---

## Revision History

- **2025-10-16**: Initial documentation of Canvas layout fix and coordinate system patterns
