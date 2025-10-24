using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Serilog;
using AGI.Kapster.Desktop.Overlays.Layers;
using AGI.Kapster.Desktop.Overlays.Layers.Selection;
using AGI.Kapster.Desktop.Services.Export;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Clipboard;
using AGI.Kapster.Desktop.Services.Export.Imaging;

namespace AGI.Kapster.Desktop.Overlays.Infrastructure;

/// <summary>
/// Implementation of IOverlayActionHandler - centralizes top-level overlay actions
/// Plan A: Decouples OverlayWindow from action execution logic
/// </summary>
public class OverlayActionHandler : IOverlayActionHandler
{
    private readonly Window _window;
    private readonly IAnnotationExportService _exportService;
    private readonly IOverlayLayerManager _layerManager;
    private readonly IOverlayImageCaptureService _imageCaptureService;
    private readonly OverlayContextProvider _contextProvider;
    private readonly Func<Size> _windowSizeGetter;
    private readonly IClipboardStrategy _clipboardStrategy;
    
    public OverlayActionHandler(
        Window window,
        IAnnotationExportService exportService,
        IOverlayLayerManager layerManager,
        IOverlayImageCaptureService imageCaptureService,
        OverlayContextProvider contextProvider,
        Func<Size> windowSizeGetter,
        IClipboardStrategy clipboardStrategy)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
        _imageCaptureService = imageCaptureService ?? throw new ArgumentNullException(nameof(imageCaptureService));
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _windowSizeGetter = windowSizeGetter ?? throw new ArgumentNullException(nameof(windowSizeGetter));
        _clipboardStrategy = clipboardStrategy ?? throw new ArgumentNullException(nameof(clipboardStrategy));
    }
    
    public async Task HandleConfirmAsync(Rect region)
    {
        Log.Debug("OverlayActionHandler: Confirm requested - saving to clipboard for region {Region}", region);
        
        try
        {
            // Get annotation layer
            var annotationLayer = _layerManager.GetLayer(LayerIds.Annotation) as IAnnotationLayer;
            if (annotationLayer == null)
            {
                Log.Warning("Cannot confirm: annotation layer not found");
                HandleCancel("Annotation layer not found");
                return;
            }
            
            // Get selection layer
            var selectionLayer = _layerManager.GetLayer(LayerIds.Selection) as ISelectionLayer;
            
            // Hide selection layer during capture
            if (selectionLayer != null)
            {
                _layerManager.HideLayer(LayerIds.Selection);
            }
            
            // End text editing before capture
            annotationLayer?.EndTextEditing();
            await Task.Delay(OverlayConstants.StandardUiDelay);
            
            // Capture image with annotations
            var capturedImage = await _imageCaptureService.CaptureWindowRegionWithAnnotationsAsync(
                region, _contextProvider.FrozenBackground, annotationLayer?.GetAnnotations(), _windowSizeGetter());
            
            if (capturedImage == null)
            {
                Log.Error("OverlayActionHandler: Failed to capture image - image is null");
                
                // Show selection layer again on error
                if (selectionLayer != null)
                {
                    _layerManager.ShowLayer(LayerIds.Selection);
                }
                
                // Don't close window, let user retry or cancel
                return;
            }
            
            // Copy to clipboard using IClipboardStrategy (same as ScreenshotServiceBase)
            var skBitmap = BitmapConverter.ConvertToSKBitmap(capturedImage);
            if (skBitmap == null)
            {
                Log.Error("OverlayActionHandler: Failed to convert image to SKBitmap");
                
                // Show selection layer again on error
                if (selectionLayer != null)
                {
                    _layerManager.ShowLayer(LayerIds.Selection);
                }
                
                // Don't close window, let user retry or cancel
                return;
            }
            
            var success = await _clipboardStrategy.SetImageAsync(skBitmap);
            skBitmap.Dispose();
            
            if (!success)
            {
                Log.Error("OverlayActionHandler: Failed to copy image to clipboard");
                
                // Show selection layer again on error
                if (selectionLayer != null)
                {
                    _layerManager.ShowLayer(LayerIds.Selection);
                }
                
                // Don't close window, let user retry or cancel
                return;
            }
            
            Log.Information("Image copied to clipboard successfully: {Region}", region);
            
            // CRITICAL: Close overlay after successful save
            // This will trigger proper cleanup chain
            _window.Close();
            
            Log.Debug("OverlayActionHandler: Confirm completed - image saved to clipboard");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OverlayActionHandler: Failed to save to clipboard");
            
            // Show selection layer again on error
            var selectionLayer = _layerManager.GetLayer(LayerIds.Selection) as ISelectionLayer;
            if (selectionLayer != null)
            {
                _layerManager.ShowLayer(LayerIds.Selection);
            }
            
            // Don't close window on exception, let user retry or cancel
            // DO NOT throw - we want to keep the window open for retry
        }
    }
    
    public async Task HandleExportAsync(Rect region)
    {
        Log.Debug("OverlayActionHandler: Export requested for region {Region}", region);
        
        try
        {
            // Get annotation layer
            var annotationLayer = _layerManager.GetLayer(LayerIds.Annotation) as IAnnotationLayer;
            if (annotationLayer == null)
            {
                Log.Warning("Cannot export: annotation layer not found");
                HandleCancel("Annotation layer not found");
                return;
            }
            
            // Get selection layer
            var selectionLayer = _layerManager.GetLayer(LayerIds.Selection) as ISelectionLayer;
            
            // Hide selection layer during export
            if (selectionLayer != null)
            {
                _layerManager.HideLayer(LayerIds.Selection);
            }
            
            // Execute export via export service
            await _exportService.HandleExportRequestAsync(
                _window,
                region,
                async () =>
                {
                    // End text editing before capture
                    annotationLayer?.EndTextEditing();
                    await Task.Delay(OverlayConstants.StandardUiDelay);

                    return await _imageCaptureService.CaptureWindowRegionWithAnnotationsAsync(
                        region, _contextProvider.FrozenBackground, annotationLayer?.GetAnnotations(), _windowSizeGetter());
                },
                () => _window.Close());
            
            // After successful export, close overlay
            _window.Close();
            
            Log.Debug("OverlayActionHandler: Export completed successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OverlayActionHandler: Export failed");
            
            // Show selection layer again on error
            var selectionLayer = _layerManager.GetLayer(LayerIds.Selection) as ISelectionLayer;
            if (selectionLayer != null)
            {
                _layerManager.ShowLayer(LayerIds.Selection);
            }
            
            throw;
        }
    }
    
    public void HandleCancel(string reason)
    {
        Log.Debug("OverlayActionHandler: Cancel requested - {Reason}", reason);
        
        // Close overlay window
        _window.Close();
    }
}

