using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Serilog;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using AGI.Captor.Desktop.Services;
using AGI.Captor.Desktop.Services.Overlay;
using AGI.Captor.Desktop.Models;
using AGI.Captor.Desktop.Dialogs;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace AGI.Captor.Desktop.Overlays;

public partial class OverlayWindow : Window
{
	private readonly IElementDetector? _elementDetector;
	private ElementHighlightOverlay? _elementHighlight;
	private bool _isElementPickerMode = false; // Default to free selection mode
	private bool _hasEditableSelection = false; // Track if there's an editable selection
	private NewAnnotationOverlay? _annotator; // Keep reference to correct annotator instance
	
	// Throttling for element detection to prevent excessive updates
	private PixelPoint _lastDetectionPos;
	private DateTime _lastDetectionTime = DateTime.MinValue;
	private const double MinMovementThreshold = 8.0; // pixels
	private static readonly TimeSpan MinDetectionInterval = TimeSpan.FromMilliseconds(30); // ~33 FPS max

	public OverlayWindow(IElementDetector? elementDetector = null)
	{
		_elementDetector = elementDetector;
		InitializeComponent();
		
		// Create settings service instance for this overlay session
		var settingsService = new SettingsService();
		
		// Replace XAML annotator with one that has settings service
		var existingAnnotator = this.FindControl<NewAnnotationOverlay>("Annotator");
		if (existingAnnotator != null)
		{
			// Create a new NewAnnotationOverlay with settings service and replace the XAML one
			var newAnnotator = new NewAnnotationOverlay(settingsService);
			newAnnotator.Name = "Annotator";
			
			// Find the parent grid and replace the old annotator
			if (this.Content is Grid grid)
			{
				var index = grid.Children.IndexOf(existingAnnotator);
				if (index >= 0)
				{
					grid.Children.RemoveAt(index);
					grid.Children.Insert(index, newAnnotator);
					
					// Update the reference for later setup
					existingAnnotator = newAnnotator;
					_annotator = newAnnotator; // Store reference to avoid FindControl issues
				}
			}
		}
		
		SetupElementHighlight();
		
		// Start in free selection mode by default
		_isElementPickerMode = false;
		_hasEditableSelection = false;
		
		if (_elementDetector != null && _elementHighlight != null)
		{
			_elementHighlight.IsActive = false; // Initially disabled
		}
		
		// Show selection overlay by default for free selection
		var selectorOverlay = this.FindControl<SelectionOverlay>("Selector");
		if (selectorOverlay != null)
		{
			selectorOverlay.IsVisible = true;
			selectorOverlay.IsHitTestVisible = true;
		}
		
		// Set initial cursor for free selection mode
		this.Cursor = new Cursor(StandardCursorType.Cross);
		
		Log.Information("OverlayWindow created with default free selection mode");
		
		Log.Information("OverlayWindow created {W}x{H}", Width, Height);
		
		// Set up mouse event handlers for element selection
		this.PointerPressed += OnOverlayPointerPressed;
		this.PointerMoved += OnOverlayPointerMoved;

		if (this.FindControl<SelectionOverlay>("Selector") is { } selector)
		{
			// Backdrop removed to avoid blur/ghosting issues

			selector.SelectionFinished += r =>
			{
				// Keep selection for annotation; don't capture yet
				_hasEditableSelection = true; // Mark that we have an editable selection
				Log.Information("Selection finished: {X},{Y} {W}x{H} - editable selection created", r.X, r.Y, r.Width, r.Height);
			};

			// Create a hole in mask over selection using Path (even-odd)
			selector.SelectionChanged += r =>
			{
				if (this.FindControl<Avalonia.Controls.Shapes.Path>("MaskPath") is { } mask)
				{
					var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
					group.Children.Add(new RectangleGeometry(new Rect(0,0,Bounds.Width, Bounds.Height)));
					if (r.Width > 0 && r.Height > 0)
						group.Children.Add(new RectangleGeometry(r));
					mask.Data = group;
				}
				if (_annotator != null)
				{
					_annotator.SelectionRect = r;
					_annotator.IsHitTestVisible = r.Width > 2 && r.Height > 2;
				}
				UpdateToolbarPosition(r);
			};

			selector.ConfirmRequested += async r =>
			{
				try
				{
					var success = await CopyRegionToClipboardAsync(r);
					Log.Information("CopyRegionToClipboard result={Success}", success);
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Capture failed");
				}
				finally
				{
					var _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
					{
						await System.Threading.Tasks.Task.Delay(50);
						Close();
					});
				}
			};
		}

		if (this.FindControl<NewAnnotationToolbar>("Toolbar") is { } toolbar && existingAnnotator != null)
		{
			// Set default tool to Arrow first
			Log.Information("Setting default tool to Arrow");
			existingAnnotator.CurrentTool = Services.AnnotationToolType.Arrow;
			Log.Information("Default tool set to: {CurrentTool}", existingAnnotator.CurrentTool);
			
			// Then set up toolbar (this will call UpdateUIFromTarget and sync the UI)
			toolbar.SetTarget(existingAnnotator);
			
			// Subscribe to export events
			existingAnnotator.ExportRequested += HandleExportRequest;
			
			// Handle double-click confirm (same as old version)
			existingAnnotator.ConfirmRequested += async r =>
			{
				try
				{
					var success = await CopyRegionToClipboardAsync(r);
					Log.Information("CopyRegionToClipboard result={Success}", success);
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Capture failed");
				}
				finally
				{
					var _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
					{
						await System.Threading.Tasks.Task.Delay(50);
						
						// Close all overlay windows, not just this one
						var overlayController = App.Services?.GetService(typeof(IOverlayController)) as IOverlayController;
						if (overlayController != null)
						{
							overlayController.CloseAll();
							Log.Information("All overlay windows closed after double-click save");
						}
						else
						{
							// Fallback: close just this window
							Close();
							Log.Warning("Could not get overlay controller, closing only current window");
						}
					});
				}
			};
		}


		// Hide toolbar initially
		if (this.FindControl<NewAnnotationToolbar>("Toolbar") is { } tb)
		{
			Canvas.SetLeft(tb, -10000);
			Canvas.SetTop(tb, -10000);
		}

		// Clear global selection state when this window closes
		this.Closing += (sender, e) =>
		{
			GlobalSelectionState.ClearSelection(this);
			Log.Information("OverlayWindow: Cleared global selection state on window close");
		};
	}

	private void UpdateToolbarPosition(Rect selection)
	{
		if (this.FindControl<NewAnnotationToolbar>("Toolbar") is not { } tb || this.FindControl<Canvas>("UiCanvas") is not { } canvas)
			return;

		if (selection.Width <= 0 || selection.Height <= 0)
		{
			Canvas.SetLeft(tb, -10000);
			Canvas.SetTop(tb, -10000);
			return;
		}

		// Desired position: slightly outside bottom-right
		double offset = 8;
		double desiredX = selection.Right + offset;
		double desiredY = selection.Bottom + offset;

		// Toolbar size estimation: measure
		tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		var size = tb.DesiredSize;

		// Window bounds
		double maxX = Bounds.Width;
		double maxY = Bounds.Height;

		// Auto-flip horizontally if overflowing right; vertically if overflowing bottom
		if (desiredX + size.Width > maxX)
		{
			desiredX = selection.X - size.Width - offset; // place to the left outside
			if (desiredX < 0)
			{
				// clamp inside selection right-bottom
				desiredX = Math.Max(selection.Right - size.Width - offset, selection.X + offset);
			}
		}
		if (desiredY + size.Height > maxY)
		{
			desiredY = selection.Y - size.Height - offset; // place above outside
			if (desiredY < 0)
			{
				// clamp inside selection right-bottom
				desiredY = Math.Max(selection.Bottom - size.Height - offset, selection.Y + offset);
			}
		}

		Canvas.SetLeft(tb, desiredX);
		Canvas.SetTop(tb, desiredY);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		
		// ESC key always exits screenshot mode
		if (e.Key == Key.Escape)
		{
			Serilog.Log.Information("ESC key pressed - exiting screenshot mode");
			Close();
			e.Handled = true;
		}
		
		if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
		{
			// CTRL key pressed - switch to auto highlight mode (only if no editable selection)
			if (!_hasEditableSelection && !_isElementPickerMode)
			{
				_isElementPickerMode = true;
				if (_elementHighlight != null)
				{
					_elementHighlight.IsActive = true;
				}
				
				// Hide selection overlay
				var selector = this.FindControl<SelectionOverlay>("Selector");
				if (selector != null)
				{
					selector.IsVisible = false;
					selector.IsHitTestVisible = false;
				}
				
				// Set cursor for element selection
				this.Cursor = new Cursor(StandardCursorType.Hand);
				
				Log.Information("CTRL pressed - switched to auto highlight mode");
			}
			e.Handled = true;
		}
		else if (e.Key == Key.Tab)
		{
			// Tab key for manual toggle (fallback)
			ToggleElementPickerMode();
			e.Handled = true;
		}
		else if (e.Key == Key.Space && _isElementPickerMode)
		{
			// Toggle between window and element detection mode
			_elementDetector?.ToggleDetectionMode();
			e.Handled = true;
		}
		else if (e.Key == Key.Enter)
		{
			if (this.FindControl<SelectionOverlay>("Selector") is { } selector && selector is not null)
			{
				var r = selector.SelectionRect;
				if (r.Width > 0)
				{
					var _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
					{
						try
						{
							var success = await CopyRegionToClipboardAsync(r);
							Log.Information("CopyRegionToClipboard result={Success}", success);
						}
						catch (Exception ex)
						{
							Log.Error(ex, "Capture failed");
						}
						finally
						{
							await System.Threading.Tasks.Task.Delay(50);
							
							// Close all overlay windows, not just this one
							var overlayController = App.Services?.GetService(typeof(IOverlayController)) as IOverlayController;
							if (overlayController != null)
							{
								overlayController.CloseAll();
								Log.Information("All overlay windows closed after Enter key save");
							}
							else
							{
								// Fallback: close just this window
								Close();
								Log.Warning("Could not get overlay controller, closing only current window");
							}
						}
					});
				}
			}
			e.Handled = true;
		}
	}

	protected override void OnKeyUp(KeyEventArgs e)
	{
		base.OnKeyUp(e);
		
		if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
		{
			// CTRL key released - switch back to free selection mode (only if in element picker mode and no editable selection)
			if (_isElementPickerMode && !_hasEditableSelection)
			{
				_isElementPickerMode = false;
				if (_elementHighlight != null)
				{
					_elementHighlight.IsActive = false;
					GlobalElementHighlightState.Instance.ClearOwner(_elementHighlight);
				}
				
				// Show selection overlay
				var selector = this.FindControl<SelectionOverlay>("Selector");
				if (selector != null)
				{
					selector.IsVisible = true;
					selector.IsHitTestVisible = true;
				}
				
				// Set cursor for free selection
				this.Cursor = new Cursor(StandardCursorType.Cross);
				
				Log.Information("CTRL released - switched back to free selection mode");
			}
			e.Handled = true;
		}
	}

	private void SetupElementHighlight()
	{
		if (_elementDetector == null) return;

		_elementHighlight = new ElementHighlightOverlay(_elementDetector);
		
		// Add to the grid after SelectionOverlay but before AnnotationOverlay
		var grid = (Grid)this.Content!;
		var selector = this.FindControl<SelectionOverlay>("Selector");
		if (selector != null)
		{
			var index = grid.Children.IndexOf(selector) + 1;
			grid.Children.Insert(index, _elementHighlight);
		}
		else
		{
			grid.Children.Add(_elementHighlight);
		}

		// Element selection is now handled directly by OverlayWindow mouse events
	}

	private void ToggleElementPickerMode()
	{
		_isElementPickerMode = !_isElementPickerMode;
		
		if (_elementHighlight != null)
		{
			_elementHighlight.IsActive = _isElementPickerMode;
		}
		
		// Hide/show selection overlay based on mode
		var selector = this.FindControl<SelectionOverlay>("Selector");
		if (selector != null)
		{
			selector.IsHitTestVisible = !_isElementPickerMode;
			selector.IsVisible = !_isElementPickerMode;
		}
		
		// Set appropriate cursor
		this.Cursor = _isElementPickerMode ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Cross);
		
		Log.Information("Element picker mode: {Active}", _isElementPickerMode);
	}

	private void OnElementSelected(DetectedElement element)
	{
		Log.Information("Element selected: {Name} - {Bounds}", element.Name, element.Bounds);
		
		// Convert element bounds to overlay coordinates and set selection
		var selector = this.FindControl<SelectionOverlay>("Selector");
		if (selector != null)
		{
			// Convert screen coordinates to window coordinates with proper bounds checking
			var screenBounds = element.Bounds;
			var overlayTopLeft = this.PointToClient(new PixelPoint((int)screenBounds.X, (int)screenBounds.Y));
			var overlayBottomRight = this.PointToClient(new PixelPoint(
				(int)(screenBounds.X + screenBounds.Width), 
				(int)(screenBounds.Y + screenBounds.Height)));
			
			var selectionRect = new Rect(
				Math.Min(overlayTopLeft.X, overlayBottomRight.X),
				Math.Min(overlayTopLeft.Y, overlayBottomRight.Y),
				Math.Abs(overlayBottomRight.X - overlayTopLeft.X),
				Math.Abs(overlayBottomRight.Y - overlayTopLeft.Y));
			
			// Show and enable selection overlay
			selector.IsVisible = true;
			selector.IsHitTestVisible = true;
			
			// Set the selection and switch back to normal mode
			selector.SetSelection(selectionRect);
			
		// Disable element picker mode and set editable selection flag
		_isElementPickerMode = false;
		_hasEditableSelection = true; // Mark that we now have an editable selection
		
		if (_elementHighlight != null)
		{
			_elementHighlight.IsActive = false;
			GlobalElementHighlightState.Instance.ClearOwner(_elementHighlight);
		}
		
		// Reset cursor to normal selection mode (let overlay handle cursor)
		// this.Cursor = new Cursor(StandardCursorType.Cross);
		
		Log.Information("Switched to selection mode with element bounds - editable selection created");
		}
	}

	private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
	{
		// Only handle mouse events in element picker mode
		if (!_isElementPickerMode || _elementDetector == null || _elementHighlight == null)
			return;

		var position = e.GetPosition(this);
		var screenPos = this.PointToScreen(position);
		
		// Throttle element detection to prevent excessive updates
		// Only detect if mouse moved significantly or enough time has passed
		if (ShouldUpdateElementDetection(screenPos))
		{
			try
			{
				var overlayHandle = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
				var element = _elementDetector.DetectElementAt((int)screenPos.X, (int)screenPos.Y, overlayHandle);
				_elementHighlight.SetCurrentElement(element);
				
				// Update last detection position and time
				_lastDetectionPos = screenPos;
				_lastDetectionTime = DateTime.UtcNow;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Error during mouse move element detection");
			}
		}
	}

	private bool ShouldUpdateElementDetection(PixelPoint currentPos)
	{
		var now = DateTime.UtcNow;
		
		// Check time throttling - don't update too frequently
		if (now - _lastDetectionTime < MinDetectionInterval)
			return false;
		
		// Check movement threshold - only update if mouse moved significantly
		var distance = Math.Sqrt(
			Math.Pow(currentPos.X - _lastDetectionPos.X, 2) + 
			Math.Pow(currentPos.Y - _lastDetectionPos.Y, 2));
		
		return distance >= MinMovementThreshold;
	}

	private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (_isElementPickerMode && _elementDetector != null)
		{
			// In element picker mode - only handle single clicks for element selection
			var position = e.GetPosition(this);
			var screenPos = this.PointToScreen(position);
			
			try
			{
				// Get element at click position with error handling, ignoring this overlay window
				var overlayHandle = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
				var element = _elementDetector.DetectElementAt((int)screenPos.X, (int)screenPos.Y, overlayHandle);
				if (element != null)
				{
					OnElementSelected(element);
					e.Handled = true;
				}
				else
				{
					Log.Warning("No element detected at click position {X}, {Y}", screenPos.X, screenPos.Y);
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error during element selection at {X}, {Y}", screenPos.X, screenPos.Y);
			}
		}
		// Note: When not in element picker mode, let SelectionOverlay handle the event for custom drag selection
	}

#region Cross-platform screenshot capture and clipboard
    /// <summary>
    /// Cross-platform clipboard copy method
    /// </summary>
    private async Task<bool> CopyRegionToClipboardAsync(Avalonia.Rect rect)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Use Windows-specific clipboard method for better compatibility
                return CopyRegionToClipboardWindows(rect);
            }
            else if (OperatingSystem.IsMacOS())
            {
                // Use macOS-specific clipboard method
                return await CopyRegionToClipboardMacOSAsync(rect);
            }
            else
            {
                // Fallback to Avalonia clipboard for other platforms
                return await CopyRegionToClipboardAvaloniaAsync(rect);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to copy region to clipboard");
            return false;
        }
    }

    /// <summary>
    /// Avalonia clipboard fallback method
    /// </summary>
    private async Task<bool> CopyRegionToClipboardAvaloniaAsync(Avalonia.Rect rect)
    {
        try
        {
            var bitmap = await CaptureRegionAsync(rect);
            if (bitmap == null)
            {
                Log.Warning("Failed to capture region for clipboard");
                return false;
            }

            // Use Avalonia's clipboard API for cross-platform support
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
            {
                Log.Warning("Clipboard not available");
                return false;
            }

            // Try multiple formats for better compatibility
            var dataObject = new Avalonia.Input.DataObject();
            
            // Try setting as bitmap directly
            dataObject.Set("image/png", bitmap);
            
            // Also try common clipboard formats
            try
            {
                // Convert bitmap to byte array for more formats
                using var stream = new System.IO.MemoryStream();
                bitmap.Save(stream);
                var imageData = stream.ToArray();
                
                dataObject.Set("PNG", imageData);
                dataObject.Set("image/png", imageData);
                dataObject.Set("CF_DIB", imageData);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to set additional clipboard formats");
            }
            
            await clipboard.SetDataObjectAsync(dataObject);
            Log.Information("Successfully copied region to clipboard using Avalonia API");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to copy region to clipboard using Avalonia API");
            return false;
        }
    }

    /// <summary>
    /// macOS-specific clipboard copy using native commands
    /// </summary>
    private async Task<bool> CopyRegionToClipboardMacOSAsync(Avalonia.Rect rect)
    {
        try
        {
            // Convert to screen coordinates
            var p1 = this.PointToScreen(new Point(rect.X, rect.Y));
            var p2 = this.PointToScreen(new Point(rect.Right, rect.Bottom));
            int x = Math.Min(p1.X, p2.X);
            int y = Math.Min(p1.Y, p2.Y);
            int w = Math.Max(1, Math.Abs(p2.X - p1.X));
            int h = Math.Max(1, Math.Abs(p2.Y - p1.Y));

            // Use screencapture with clipboard option
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "screencapture",
                    Arguments = $"-R {x},{y},{w},{h} -c",  // -c flag copies to clipboard
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                Log.Information("Successfully copied region to clipboard using screencapture -c");
                return true;
            }
            else
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                Log.Error("screencapture clipboard command failed with exit code: {ExitCode}, Error: {Error}", 
                    process.ExitCode, stderr);
                
                // Fallback to file-based approach
                return await CopyRegionToClipboardMacOSFileAsync(rect);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to copy region to clipboard using screencapture");
            
            // Fallback to file-based approach
            return await CopyRegionToClipboardMacOSFileAsync(rect);
        }
    }

    /// <summary>
    /// macOS clipboard fallback using temporary file and pbcopy
    /// </summary>
    private async Task<bool> CopyRegionToClipboardMacOSFileAsync(Avalonia.Rect rect)
    {
        try
        {
            var bitmap = await CaptureRegionAsync(rect);
            if (bitmap == null)
            {
                Log.Warning("Failed to capture region for clipboard");
                return false;
            }

            // Save to temporary file
            var tempFile = System.IO.Path.GetTempFileName() + ".png";
            
            try
            {
                bitmap.Save(tempFile);
                
                // Use osascript to copy image to clipboard
                var osascriptCommand = $"set the clipboard to (read file POSIX file \"{tempFile}\" as «class PNGf»)";
                
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "osascript",
                        Arguments = $"-e '{osascriptCommand}'",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    Log.Information("Successfully copied region to clipboard using osascript");
                    return true;
                }
                else
                {
                    var stderr = await process.StandardError.ReadToEndAsync();
                    Log.Error("osascript clipboard command failed with exit code: {ExitCode}, Error: {Error}", 
                        process.ExitCode, stderr);
                    return false;
                }
            }
            finally
            {
                try { System.IO.File.Delete(tempFile); } catch { }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to copy region to clipboard using osascript");
            return false;
        }
    }

    /// <summary>
    /// Cross-platform screenshot capture method
    /// </summary>
    private async Task<Bitmap?> CaptureRegionAsync(Avalonia.Rect rect)
    {
        if (OperatingSystem.IsWindows())
        {
            return CaptureRegionWindows(rect);
        }
        else if (OperatingSystem.IsMacOS())
        {
            return await CaptureRegionMacOSAsync(rect);
        }
        else
        {
            throw new PlatformNotSupportedException("Screenshot capture is not supported on this platform");
        }
    }

    /// <summary>
    /// macOS screenshot capture using screencapture command
    /// </summary>
    private async Task<Bitmap?> CaptureRegionMacOSAsync(Avalonia.Rect rect)
    {
        try
        {
            // Convert to screen coordinates
            var p1 = this.PointToScreen(new Point(rect.X, rect.Y));
            var p2 = this.PointToScreen(new Point(rect.Right, rect.Bottom));
            int x = Math.Min(p1.X, p2.X);
            int y = Math.Min(p1.Y, p2.Y);
            int w = Math.Max(1, Math.Abs(p2.X - p1.X));
            int h = Math.Max(1, Math.Abs(p2.Y - p1.Y));

            // Create temporary file for screenshot
            var tempFile = System.IO.Path.GetTempFileName() + ".png";
            
            // Use screencapture command to capture region
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "screencapture",
                    Arguments = $"-R {x},{y},{w},{h} \"{tempFile}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && System.IO.File.Exists(tempFile))
            {
                try
                {
                    using var fileStream = System.IO.File.OpenRead(tempFile);
                    var bitmap = new Bitmap(fileStream);
                    return bitmap;
                }
                finally
                {
                    try { System.IO.File.Delete(tempFile); } catch { }
                }
            }
            else
            {
                Log.Error("screencapture command failed with exit code: {ExitCode}", process.ExitCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to capture screenshot on macOS");
            return null;
        }
    }

#region Windows GDI capture
    private Bitmap? CaptureRegionWindows(Avalonia.Rect rect)
    {
        // Convert both corners to screen pixels to handle DPI/multi-screen correctly
        var p1 = this.PointToScreen(new Point(rect.X, rect.Y));
        var p2 = this.PointToScreen(new Point(rect.Right, rect.Bottom));
        int x = Math.Min(p1.X, p2.X);
        int y = Math.Min(p1.Y, p2.Y);
        int w = Math.Max(1, Math.Abs(p2.X - p1.X));
        int h = Math.Max(1, Math.Abs(p2.Y - p1.Y));

        IntPtr hScreenDC = GetDC(IntPtr.Zero);
        IntPtr hMemDC = CreateCompatibleDC(hScreenDC);
        IntPtr hBitmap = CreateCompatibleBitmap(hScreenDC, w, h);
        IntPtr hOld = SelectObject(hMemDC, hBitmap);
        _ = BitBlt(hMemDC, 0, 0, w, h, hScreenDC, x, y, SRCCOPY);
        _ = SelectObject(hMemDC, hOld);
        _ = DeleteDC(hMemDC);
        _ = ReleaseDC(IntPtr.Zero, hScreenDC);

        if (hBitmap == IntPtr.Zero)
            return null;

        try
        {
            using var stream = new System.IO.MemoryStream();
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                using (var bmp = System.Drawing.Image.FromHbitmap(hBitmap))
                {
                    bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                }
                stream.Position = 0;
                return new Bitmap(stream);
            }
            else
            {
                throw new PlatformNotSupportedException("This operation requires Windows 6.1 or later");
            }
        }
        finally
        {
            _ = DeleteObject(hBitmap);
        }
    }

    private bool CopyRegionToClipboardWindows(Avalonia.Rect rect)
    {
        // Convert to screen pixel coordinates using both corners (DPI-safe)
        var p1 = this.PointToScreen(new Point(rect.X, rect.Y));
        var p2 = this.PointToScreen(new Point(rect.Right, rect.Bottom));
        int x = Math.Min(p1.X, p2.X);
        int y = Math.Min(p1.Y, p2.Y);
        int w = Math.Max(1, Math.Abs(p2.X - p1.X));
        int h = Math.Max(1, Math.Abs(p2.Y - p1.Y));

        IntPtr hScreenDC = GetDC(IntPtr.Zero);
        if (hScreenDC == IntPtr.Zero) return false;
        IntPtr hMemDC = CreateCompatibleDC(hScreenDC);
        if (hMemDC == IntPtr.Zero) { ReleaseDC(IntPtr.Zero, hScreenDC); return false; }
        IntPtr hBitmap = CreateCompatibleBitmap(hScreenDC, w, h);
        if (hBitmap == IntPtr.Zero) { DeleteDC(hMemDC); ReleaseDC(IntPtr.Zero, hScreenDC); return false; }
        IntPtr hOld = SelectObject(hMemDC, hBitmap);
        bool blt = BitBlt(hMemDC, 0, 0, w, h, hScreenDC, x, y, SRCCOPY);
        _ = SelectObject(hMemDC, hOld);
        _ = DeleteDC(hMemDC);
        _ = ReleaseDC(IntPtr.Zero, hScreenDC);
        if (!blt)
        {
            DeleteObject(hBitmap);
            return false;
        }

        bool result = false;
        // Robust clipboard open with small retries, as other apps may temporarily lock
        for (int i = 0; i < 10 && !result; i++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    EmptyClipboard();
                    IntPtr set = SetClipboardData(CF_BITMAP, hBitmap);
                    result = set != IntPtr.Zero;
                    if (result)
                    {
                        hBitmap = IntPtr.Zero; // Clipboard takes ownership
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }
            if (!result)
            {
                System.Threading.Thread.Sleep(25);
            }
        }

        if (hBitmap != IntPtr.Zero)
        {
            DeleteObject(hBitmap);
        }

        return result;
    }

    private const int SRCCOPY = 0x00CC0020;
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, int rop);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    private const uint CF_BITMAP = 2;
#endregion

#endregion

#region Export Functionality

    /// <summary>
    /// Handle export request from annotation overlay
    /// </summary>
    private async void HandleExportRequest()
    {
        try
        {
            if (_annotator == null)
                return;

            var selectionRect = _annotator.SelectionRect;
            if (selectionRect.Width <= 0 || selectionRect.Height <= 0)
            {
                Log.Warning("Cannot export: no valid selection area");
                return;
            }

            // Show export settings dialog
            var exportService = new ExportService();
            var defaultSettings = exportService.GetDefaultSettings();
            var imageSize = new Avalonia.Size(selectionRect.Width, selectionRect.Height);
            var settingsDialog = new ExportSettingsDialog(defaultSettings, imageSize);
            
            var dialogResult = await settingsDialog.ShowDialog<bool?>(this);
            if (dialogResult != true)
            {
                Log.Information("Export cancelled by user");
                return;
            }

            var settings = settingsDialog.Settings;
            
            // Show save file dialog with appropriate file types
            var storageProvider = GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null)
            {
                Log.Error("Cannot access storage provider for file dialog");
                return;
            }

            var fileTypes = CreateFileTypesFromFormats(exportService.GetSupportedFormats());
            var suggestedFileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}{settings.GetFileExtension()}";

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Annotated Screenshot",
                FileTypeChoices = fileTypes,
                DefaultExtension = settings.GetFileExtension().TrimStart('.'),
                SuggestedFileName = suggestedFileName
            });

            if (file == null) 
            {
                Log.Information("File save cancelled by user");
                return;
            }

            // Show progress dialog and perform export
            var progressDialog = new ExportProgressDialog();
            progressDialog.SetFileInfo(System.IO.Path.GetFileName(file.Path.LocalPath), settings.Format.ToString());
            
            // Show progress dialog without blocking
            var progressTask = progressDialog.ShowDialog(this);
            
            // Hide selection border before capturing screenshot
            var selector = this.FindControl<SelectionOverlay>("Selector");
            var wasVisible = selector?.IsVisible ?? false;
            
            try
            {
                progressDialog.UpdateProgress(5, "Preparing capture...");
                
                if (selector != null)
                {
                    selector.IsVisible = false;
                    // Give a moment for the UI to update
                    await Task.Delay(50);
                }
                
                try
                {
                    // Capture screenshot without selection border
                    progressDialog.UpdateProgress(10, "Capturing screenshot...");
                    var screenshot = await CaptureRegionAsync(selectionRect);
                    if (screenshot == null)
                    {
                        throw new InvalidOperationException("Failed to capture screenshot");
                    }

                var annotations = _annotator.GetAnnotationService().Manager.Items;
                
                // Export with progress callback - ensure UI thread safety
                await exportService.ExportToFileAsync(screenshot, annotations, selectionRect, file.Path.LocalPath, settings, 
                    (percentage, status) => 
                    {
                        // Ensure progress updates are always dispatched to UI thread
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                            progressDialog.UpdateProgress(percentage, status),
                            Avalonia.Threading.DispatcherPriority.Background);
                    });
                
                // Close progress dialog and overlay window immediately after successful export
                progressDialog.Close();
                Log.Information("Successfully exported annotated screenshot to {FilePath} with settings: {Format}, Quality: {Quality}", 
                    file.Path.LocalPath, settings.Format, settings.Quality);
                
                    // Close overlay window immediately
                    Close();
                }
                finally
                {
                    // Restore selection border visibility
                    if (selector != null && wasVisible)
                    {
                        selector.IsVisible = true;
                    }
                }
            }
            catch (Exception exportEx)
            {
                // Restore selection border visibility on error
                if (selector != null && wasVisible)
                {
                    selector.IsVisible = true;
                }
                
                var errorMessage = exportEx.InnerException?.Message ?? exportEx.Message;
                progressDialog.ShowError($"Export failed: {errorMessage}");
                Log.Error(exportEx, "Failed to export screenshot");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to handle export request");
        }
    }

    /// <summary>
    /// Create file types for file picker from supported export formats
    /// </summary>
    private FilePickerFileType[] CreateFileTypesFromFormats(ExportFormat[] formats)
    {
        var fileTypes = new List<FilePickerFileType>();
        
        foreach (var format in formats)
        {
            var extension = GetExtensionForFormat(format);
            var description = GetDescriptionForFormat(format);
            
            fileTypes.Add(new FilePickerFileType(description)
            {
                Patterns = new[] { $"*{extension}" }
            });
        }
        
        // Add "All Supported Images" option
        var allPatterns = formats.Select(f => $"*{GetExtensionForFormat(f)}").ToArray();
        fileTypes.Insert(0, new FilePickerFileType("All Supported Images")
        {
            Patterns = allPatterns
        });
        
        return fileTypes.ToArray();
    }
    
    private string GetExtensionForFormat(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.PNG => ".png",
            ExportFormat.JPEG => ".jpg",
            ExportFormat.BMP => ".bmp",
            ExportFormat.TIFF => ".tiff",
            ExportFormat.WebP => ".webp",
            ExportFormat.GIF => ".gif",
            _ => ".png"
        };
    }
    
    private string GetDescriptionForFormat(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.PNG => "PNG Image",
            ExportFormat.JPEG => "JPEG Image",
            ExportFormat.BMP => "Bitmap Image",
            ExportFormat.TIFF => "TIFF Image",
            ExportFormat.WebP => "WebP Image",
            ExportFormat.GIF => "GIF Image",
            _ => "Image File"
        };
    }

#endregion
}
