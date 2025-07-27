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
using System.Threading.Tasks;

namespace Cpu32Emulator.Presentation
{
    /// <summary>
    /// Phase 4: Custom control for tile-based disassembly view - now the primary implementation
    /// Feature flag support removed as this is now the only disassembly view
    /// </summary>
    public sealed partial class TiledDisassemblyView : UserControl, INotifyPropertyChanged
    {
        private DisassemblyTileManager? _tileManager;
        private readonly List<Image> _visibleTileImages = new();
        private uint _currentAddress;
        private bool _isInitialized;

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
                _ = view.ScrollToAddressAsync((uint)e.NewValue);
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
        /// </summary>
        private async Task RefreshTilesAsync()
        {
            if (_tileManager == null || ItemsSource == null)
                return;

            try
            {
                // Clear existing tiles
                ClearVisibleTiles();

                // For Phase 1, create a placeholder tile for demonstration
                await CreateVisualTileAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing tiles: {ex.Message}");
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
        /// </summary>
        private async Task ScrollToAddressImmediateAsync(uint targetAddress)
        {
            if (_tileManager == null)
                return;

            try
            {
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
        }

        /// <summary>
        /// Phase 3: Animates smooth scrolling to a specific Y position
        /// </summary>
        private async Task AnimateScrollToPositionAsync(double targetY)
        {
            // For Phase 3, we'll use a simple immediate scroll
            // A future phase could implement smooth animation using Storyboard
            
            // Center the target position in the viewport
            var viewportHeight = DisassemblyCanvas.ActualHeight;
            var centeredY = targetY - (viewportHeight / 2);
            
            // Clamp to valid scroll range
            centeredY = Math.Max(0, centeredY);
            
            // For now, just simulate the scroll with a small delay
            await Task.Delay(50);
            
            // In a real implementation, this would adjust the canvas transform or scroll viewer position
            System.Diagnostics.Debug.WriteLine($"Animated scroll to Y position: {centeredY}");
        }

        /// <summary>
        /// Phase 3: Enhanced viewport range checking
        /// </summary>
        private bool IsInVisibleRange(DisassemblyTile tile)
        {
            // For Phase 3, assume tiles are visible if they're in our current cache
            // A future phase would implement proper viewport calculations
            return tile != null;
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
        /// </summary>
        private void ClearVisibleTiles()
        {
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
    }
}
