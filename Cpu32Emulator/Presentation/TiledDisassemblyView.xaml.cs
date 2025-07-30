using Cpu32Emulator.Models;
using Cpu32Emulator.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;

namespace Cpu32Emulator.Presentation
{
    /// <summary>
    /// Phase 4: Custom control for tile-based disassembly view - now the primary implementation
    /// Feature flag support removed as this is now the only disassembly view
    /// </summary>
    public sealed partial class TiledDisassemblyView : UserControl, INotifyPropertyChanged, IDisposable
    {
        private DisassemblyTileManager? _tileManager;
        private readonly List<Image> _visibleTileImages = new();
        private uint _currentAddress;
        private bool _isInitialized;
        private readonly SemaphoreSlim _uiUpdateSemaphore = new(1, 1); // Prevent concurrent UI updates
        private bool _disposed;

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(ObservableCollection<DisassemblyLineViewModel>),
                typeof(TiledDisassemblyView),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty CurrentAddressProperty =
            DependencyProperty.Register(
                nameof(CurrentAddress),
                typeof(uint),
                typeof(TiledDisassemblyView),
                new PropertyMetadata(0u, OnCurrentAddressChanged));

        public TiledDisassemblyView()
        {
            this.InitializeComponent();
            InitializeTileManager();
        }

        /// <summary>
        /// Data source for disassembly lines (compatibility with existing ListView)
        /// </summary>
        public ObservableCollection<DisassemblyLineViewModel>? ItemsSource
        {
            get => (ObservableCollection<DisassemblyLineViewModel>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>
        /// Current program counter address for centering
        /// </summary>
        public uint CurrentAddress
        {
            get => (uint)GetValue(CurrentAddressProperty);
            set => SetValue(CurrentAddressProperty, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TiledDisassemblyView view)
            {
                view.OnItemsSourceChanged();
            }
        }

        private static void OnCurrentAddressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TiledDisassemblyView view && view._isInitialized)
            {
                var newAddress = (uint)e.NewValue;
                var oldAddress = (uint)e.OldValue;
                
                // Try to update highlighting in existing tiles first
                if (view.TryUpdateCurrentInstructionHighlighting(oldAddress, newAddress))
                {
                    // Successfully updated existing tiles, no need to scroll/regenerate
                    return;
                }
                
                // Address not in current tiles, need to scroll to new address
                _ = view.ScrollToAddressAsync(newAddress);
            }
        }

        private void OnItemsSourceChanged()
        {
            if (_tileManager != null)
            {
                // Regenerate tiles when data source changes
                _ = RefreshTilesAsync();
            }
        }

        private void InitializeTileManager()
        {
            // For Phase 1, we'll inject the DisassemblyService later
            // Phase 2 will handle proper dependency injection
            _tileManager = new DisassemblyTileManager();
        }

        private void Canvas_Loaded(object sender, RoutedEventArgs e)
        {
            _isInitialized = true;
            _ = InitializeViewAsync();
        }

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isInitialized)
            {
                // Viewport size changed - may need to load additional tiles
                _ = UpdateVisibleTilesAsync();
            }
        }

        /// <summary>
        /// Initializes the tile view with initial data
        /// </summary>
        private async Task InitializeViewAsync()
        {
            if (_tileManager == null)
            {
                InitializeTileManager();
            }

            await RefreshTilesAsync();
        }

        /// <summary>
        /// Refreshes all tiles based on current data source
        /// Adds semaphore protection to prevent race conditions that cause duplicate dependency property exceptions
        /// </summary>
        private async Task RefreshTilesAsync()
        {
            if (_disposed || _tileManager == null || ItemsSource == null)
                return;

            // Use semaphore to prevent concurrent UI updates that can cause duplicate dependency property errors
            await _uiUpdateSemaphore.WaitAsync();
            try
            {
                if (_disposed) return; // Double-check after acquiring semaphore

                // Clear existing tiles
                ClearVisibleTiles();

                // For Phase 1, create a placeholder tile for demonstration
                await CreateVisualTileAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing tiles: {ex.Message}");
            }
            finally
            {
                if (!_disposed)
                    _uiUpdateSemaphore.Release();
            }
        }

        /// <summary>
        /// Phase 3: Creates a visual tile with actual data and full feature support including breakpoints and current instruction highlighting
        /// </summary>
        private async Task CreateVisualTileAsync()
        {
            if (_tileManager == null || ItemsSource == null || !ItemsSource.Any())
                return;

            try
            {
                // Clear existing tiles
                ClearVisibleTiles();

                // For Phase 3, we'll create a sample tile with actual data and pass the data source for state info
                // Get the first few entries for demonstration
                var sampleEntries = ItemsSource.Take(20).ToList();
                
                // Convert DisassemblyLineViewModel to LstEntry for the tile manager
                var lstEntries = sampleEntries.Select(line => new LstEntry
                {
                    Address = ParseAddress(line.Address),
                    SymbolName = string.IsNullOrEmpty(line.Symbol) ? null : line.Symbol,
                    Instruction = line.Instruction
                }).ToList();

                if (_tileManager != null && lstEntries.Any())
                {
                    // Create a visual preview of the tile with full data source for breakpoint/current instruction support
                    var tilePreview = _tileManager.CreateTilePreview(lstEntries, 800, 18, ItemsSource);
                    
                    // Position the tile on the canvas
                    Canvas.SetLeft(tilePreview, 0);
                    Canvas.SetTop(tilePreview, 0);

                    // Add to canvas
                    DisassemblyCanvas.Children.Add(tilePreview);
                    
                    // Update the Canvas size based on the tile's actual size
                    // We need to wait for the tile to be measured and arranged
                    tilePreview.Loaded += (sender, e) =>
                    {
                        if (sender is FrameworkElement element)
                        {
                            // Set Canvas height to accommodate the tile content
                            var requiredHeight = element.ActualHeight;
                            if (requiredHeight > 0)
                            {
                                DisassemblyCanvas.Height = Math.Max(requiredHeight, TileScrollViewer.ViewportHeight);
                                System.Diagnostics.Debug.WriteLine($"Canvas height set to: {DisassemblyCanvas.Height} (tile: {requiredHeight}, viewport: {TileScrollViewer.ViewportHeight})");
                            }
                        }
                    };
                    
                    // Also set an initial height estimate based on line count
                    var estimatedHeight = lstEntries.Count * 18; // lineHeight
                    DisassemblyCanvas.Height = Math.Max(estimatedHeight, 400); // Minimum height for scrolling
                }

                await Task.Delay(100); // Simulate processing time
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating visual tile: {ex.Message}");
            }
        }

        /// <summary>
        /// Phase 3: Scrolls smoothly to the current instruction address
        /// Equivalent to ScrollToCurrentInstruction() in the ListView implementation
        /// </summary>
        public async Task ScrollToCurrentInstructionAsync()
        {
            if (_tileManager == null || ItemsSource == null)
                return;

            try
            {
                // Find the current instruction in our data source
                var currentInstruction = ItemsSource.FirstOrDefault(line => line.IsCurrentInstruction);
                if (currentInstruction != null)
                {
                    var targetAddress = ParseAddress(currentInstruction.Address);
                    await ScrollToAddressAsync(targetAddress);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scrolling to current instruction: {ex.Message}");
            }
        }

        /// <summary>
        /// Phase 3: Scrolls smoothly to a specific address
        /// </summary>
        public async Task ScrollToAddressAsync(uint targetAddress)
        {
            if (_tileManager == null)
                return;

            try
            {
                // Check if we already have a tile containing this address
                var targetTile = _tileManager.FindTileContaining(targetAddress);
                
                if (targetTile != null && IsInVisibleRange(targetTile))
                {
                    // Address is in a visible tile - smooth scroll to it
                    if (targetTile.AddressYCoordinates.TryGetValue(targetAddress, out double yPosition))
                    {
                        await AnimateScrollToPositionAsync(yPosition);
                    }
                }
                else
                {
                    // Address is not visible - generate/load the required tile and jump to it
                    await ScrollToAddressImmediateAsync(targetAddress);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scrolling to address 0x{targetAddress:X8}: {ex.Message}");
            }
        }

        /// <summary>
        /// Phase 3: Immediately jumps to a specific address (outside visible range)
        /// Adds semaphore protection to prevent race conditions during Canvas updates
        /// </summary>
        private async Task ScrollToAddressImmediateAsync(uint targetAddress)
        {
            if (_disposed || _tileManager == null)
                return;

            // Use semaphore to prevent concurrent UI updates that can cause duplicate dependency property errors
            await _uiUpdateSemaphore.WaitAsync();
            try
            {
                if (_disposed) return; // Double-check after acquiring semaphore

                // Get or create the tile containing this address (reuses existing tiles if available)
                var requiredTile = await _tileManager.GetOrGenerateTileAsync(targetAddress, ItemsSource);
                if (requiredTile != null)
                {
                    // Clear existing tiles and display the new one
                    ClearVisibleTiles();
                    
                    // Create the visual element if needed
                    if (requiredTile.TileElement != null)
                    {
                        Canvas.SetLeft(requiredTile.TileElement, 0);
                        Canvas.SetTop(requiredTile.TileElement, 0);
                        DisassemblyCanvas.Children.Add(requiredTile.TileElement);
                        
                        // Update Canvas height to accommodate the tile
                        var tileHeight = requiredTile.TileHeight > 0 ? requiredTile.TileHeight : 400;
                        DisassemblyCanvas.Height = Math.Max(tileHeight, TileScrollViewer.ViewportHeight);
                        System.Diagnostics.Debug.WriteLine($"Canvas height set to: {DisassemblyCanvas.Height} for tile height: {tileHeight}");
                    }

                    // Center the target address in the viewport
                    if (requiredTile.AddressYCoordinates.TryGetValue(targetAddress, out double yPosition))
                    {
                        await AnimateScrollToPositionAsync(yPosition);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error jumping to address 0x{targetAddress:X8}: {ex.Message}");
            }
            finally
            {
                if (!_disposed)
                    _uiUpdateSemaphore.Release();
            }
        }

        /// <summary>
        /// Phase 3: Animates smooth scrolling to a specific Y position
        /// </summary>
        private async Task AnimateScrollToPositionAsync(double targetY)
        {
            if (TileScrollViewer == null)
                return;

            // Center the target position in the viewport
            var viewportHeight = TileScrollViewer.ViewportHeight;
            var centeredY = targetY - (viewportHeight / 2);
            
            // Clamp to valid scroll range
            var maxScrollY = Math.Max(0, DisassemblyCanvas.ActualHeight - viewportHeight);
            centeredY = Math.Max(0, Math.Min(centeredY, maxScrollY));
            
            // Use the ScrollViewer to scroll to the centered position
            TileScrollViewer.ChangeView(null, centeredY, null, false);
            
            // Small delay to allow the scroll to complete
            await Task.Delay(50);
            
            System.Diagnostics.Debug.WriteLine($"Scrolled to centered Y position: {centeredY} for target Y: {targetY}");
        }

        /// <summary>
        /// Phase 3: Enhanced viewport range checking
        /// </summary>
        private bool IsInVisibleRange(DisassemblyTile tile)
        {
            if (tile?.TileElement == null || TileScrollViewer == null)
                return false;

            // Get the tile's position and dimensions
            var tileTop = Canvas.GetTop(tile.TileElement);
            var tileHeight = tile.TileElement.ActualHeight;
            var tileBottom = tileTop + tileHeight;

            // Get the current scroll position and viewport dimensions
            var viewportTop = TileScrollViewer.VerticalOffset;
            var viewportBottom = viewportTop + TileScrollViewer.ViewportHeight;

            // Check if the tile overlaps with the viewport
            return tileBottom > viewportTop && tileTop < viewportBottom;
        }

        /// <summary>
        /// Parses an address string to uint
        /// </summary>
        private uint ParseAddress(string addressString)
        {
            if (string.IsNullOrEmpty(addressString))
                return 0;

            // Remove "0x" prefix if present
            var cleanAddress = addressString.Replace("0x", "").Replace("0X", "");
            
            if (uint.TryParse(cleanAddress, System.Globalization.NumberStyles.HexNumber, null, out uint address))
            {
                return address;
            }
            
            return 0;
        }

        /// <summary>
        /// Updates visible tiles based on current viewport
        /// </summary>
        private async Task UpdateVisibleTilesAsync()
        {
            if (_tileManager == null)
                return;

            await Task.Delay(1); // Placeholder for Phase 1
            
            // Phase 2 will implement proper viewport management
        }

        /// <summary>
        /// Clears all visible tiles from the canvas
        /// Ensures UI operations are performed on the UI thread to prevent dependency property errors
        /// </summary>
        private void ClearVisibleTiles()
        {
            // Ensure we're on the UI thread when modifying UI elements
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => ClearVisibleTiles());
                return;
            }

            DisassemblyCanvas.Children.Clear();
            _visibleTileImages.Clear();
        }

        /// <summary>
        /// Sets the DisassemblyService for tile generation
        /// Temporary method for Phase 1 - Phase 2 will use proper DI
        /// </summary>
        public void SetDisassemblyService(DisassemblyService disassemblyService)
        {
            _tileManager = new DisassemblyTileManager(disassemblyService);
            
            if (_isInitialized)
            {
                _ = RefreshTilesAsync();
            }
        }

        /// <summary>
        /// Gets the current tile manager for debugging
        /// </summary>
        public DisassemblyTileManager? GetTileManager() => _tileManager;

        /// <summary>
        /// Attempts to update current instruction highlighting in existing visible tiles
        /// without regenerating the entire view
        /// </summary>
        /// <param name="oldAddress">Previous PC address to unhighlight</param>
        /// <param name="newAddress">New PC address to highlight</param>
        /// <returns>True if highlighting was updated, false if tiles need to be regenerated</returns>
        private bool TryUpdateCurrentInstructionHighlighting(uint oldAddress, uint newAddress)
        {
            try
            {
                // Check if we have any visible tiles in the canvas
                if (DisassemblyCanvas.Children.Count == 0)
                    return false;

                bool foundNewAddress = false;
                bool foundOldAddress = false;

                // Iterate through all visible tiles (should typically be just one)
                foreach (var child in DisassemblyCanvas.Children)
                {
                    if (child is FrameworkElement tileElement)
                    {
                        // Update highlighting in this tile
                        var result = UpdateHighlightingInTile(tileElement, oldAddress, newAddress);
                        foundOldAddress |= result.foundOld;
                        foundNewAddress |= result.foundNew;
                    }
                }

                // Return true only if we found the new address (can highlight it)
                // We don't require finding the old address since it might not be visible
                return foundNewAddress;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating instruction highlighting: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates highlighting within a specific tile element
        /// </summary>
        private (bool foundOld, bool foundNew) UpdateHighlightingInTile(FrameworkElement tileElement, uint oldAddress, uint newAddress)
        {
            bool foundOld = false;
            bool foundNew = false;

            // The tile structure is a StackPanel containing Grid elements for each line
            if (tileElement is StackPanel stackPanel)
            {
                foreach (var child in stackPanel.Children)
                {
                    if (child is Grid lineGrid && lineGrid.Tag is uint lineAddress)
                    {
                        // Check if this line matches either address
                        if (lineAddress == oldAddress)
                        {
                            RemoveCurrentInstructionHighlight(lineGrid);
                            foundOld = true;
                        }
                        else if (lineAddress == newAddress)
                        {
                            AddCurrentInstructionHighlight(lineGrid);
                            foundNew = true;
                        }
                    }
                }
            }

            return (foundOld, foundNew);
        }

        /// <summary>
        /// Adds current instruction highlight to a line grid
        /// </summary>
        private void AddCurrentInstructionHighlight(Grid lineGrid)
        {
            // Find the indicator panel (first column)
            if (lineGrid.Children.FirstOrDefault() is StackPanel indicatorPanel)
            {
                // Remove any existing arrow first
                var existingArrow = indicatorPanel.Children.OfType<TextBlock>()
                    .FirstOrDefault(tb => tb.Text == "►");
                if (existingArrow != null)
                    indicatorPanel.Children.Remove(existingArrow);

                // Add the current instruction arrow
                var arrow = new TextBlock
                {
                    Text = "►",
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Yellow),
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold
                };
                indicatorPanel.Children.Add(arrow);

                // Optionally add background highlight
                lineGrid.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 0)); // Semi-transparent yellow
            }
        }

        /// <summary>
        /// Removes current instruction highlight from a line grid
        /// </summary>
        private void RemoveCurrentInstructionHighlight(Grid lineGrid)
        {
            // Find the indicator panel (first column)
            if (lineGrid.Children.FirstOrDefault() is StackPanel indicatorPanel)
            {
                // Remove the arrow indicator
                var arrow = indicatorPanel.Children.OfType<TextBlock>()
                    .FirstOrDefault(tb => tb.Text == "►");
                if (arrow != null)
                    indicatorPanel.Children.Remove(arrow);

                // Remove background highlight
                lineGrid.Background = null;
            }
        }

        /// <summary>
        /// Dispose of resources to prevent memory leaks and race conditions
        /// </summary>
        public new void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _uiUpdateSemaphore?.Dispose();
            base.Dispose();
        }
    }
}
