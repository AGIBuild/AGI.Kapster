using System;
using AGI.Kapster.Desktop.Models;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Overlay;

/// <summary>
/// Global singleton to manage element highlighting across multiple overlay windows
/// Prevents multiple highlights from appearing on different screens
/// </summary>
public sealed class GlobalElementHighlightState
{
    private static readonly Lazy<GlobalElementHighlightState> _instance =
        new(() => new GlobalElementHighlightState());

    public static GlobalElementHighlightState Instance => _instance.Value;

    private DetectedElement? _currentElement;
    private object? _currentHighlightOwner;

    private GlobalElementHighlightState() { }

    /// <summary>
    /// Updates the current global highlighted element
    /// Only allows one highlight across all overlay windows
    /// </summary>
    /// <param name="element">Element to highlight (null to clear)</param>
    /// <param name="owner">The overlay window requesting the highlight</param>
    /// <returns>True if this owner should show the highlight, false otherwise</returns>
    public bool SetCurrentElement(DetectedElement? element, object owner)
    {
        if (element == null)
        {
            // Clear highlight from this owner
            if (_currentHighlightOwner == owner)
            {
                _currentElement = null;
                _currentHighlightOwner = null;
                Log.Debug("Cleared global element highlight from owner: {Owner}", owner.GetHashCode());
                return true; // Owner should clear its highlight
            }
            return false; // No change needed for non-owners
        }

        // Check if this is the same element (avoid unnecessary updates)
        if (_currentElement != null && AreElementsEqual(_currentElement, element))
        {
            // Same element, but check if this is a different owner trying to highlight
            if (_currentHighlightOwner != owner)
            {
                Log.Debug("Different overlay tried to highlight same element - blocking. Current owner: {Current}, New owner: {New}",
                    _currentHighlightOwner?.GetHashCode(), owner.GetHashCode());
                return false; // Block this owner from showing highlight
            }
            return true; // Same owner, same element - continue showing
        }

        // New element - check if someone else owns the highlight
        if (_currentHighlightOwner != null && _currentHighlightOwner != owner)
        {
            Log.Debug("New element from different owner - taking over highlight. Previous: {Previous}, New: {New}",
                _currentHighlightOwner.GetHashCode(), owner.GetHashCode());
        }

        // Update global state
        _currentElement = element;
        _currentHighlightOwner = owner;

        Log.Debug("Updated global element highlight: {Name} ({ClassName}) by owner {Owner}",
            element.Name, element.ClassName, owner.GetHashCode());

        return true; // This owner should show the highlight
    }

    /// <summary>
    /// Checks if the given owner currently owns the highlight
    /// </summary>
    public bool IsCurrentOwner(object owner)
    {
        return _currentHighlightOwner == owner;
    }

    /// <summary>
    /// Gets the current highlighted element
    /// </summary>
    public DetectedElement? CurrentElement => _currentElement;

    /// <summary>
    /// Clears any highlight owned by the specified owner
    /// </summary>
    public void ClearOwner(object owner)
    {
        if (_currentHighlightOwner == owner)
        {
            _currentElement = null;
            _currentHighlightOwner = null;
            Log.Debug("Cleared highlight owner: {Owner}", owner.GetHashCode());
        }
    }

    /// <summary>
    /// Forces clear of all highlights (for debugging/reset)
    /// </summary>
    public void ForceCleanAll()
    {
        _currentElement = null;
        _currentHighlightOwner = null;
        Log.Debug("Force cleared all highlights");
    }

    private static bool AreElementsEqual(DetectedElement a, DetectedElement b)
    {
        return a.WindowHandle == b.WindowHandle &&
               a.ClassName == b.ClassName &&
               Math.Abs(a.Bounds.X - b.Bounds.X) < 5 &&
               Math.Abs(a.Bounds.Y - b.Bounds.Y) < 5 &&
               Math.Abs(a.Bounds.Width - b.Bounds.Width) < 5 &&
               Math.Abs(a.Bounds.Height - b.Bounds.Height) < 5;
    }
}
