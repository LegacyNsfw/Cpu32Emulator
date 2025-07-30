using Cpu32Emulator.Models;
using Cpu32Emulator.Presentation;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cpu32Emulator.Services;

/// <summary>
/// Phase 3: Manages creation, caching, and retrieval of disassembly tiles with full feature support
/// Supports breakpoint visualization, current instruction highlighting, and smooth scrolling
/// </summary>
public class DisassemblyTileManager
{
    private readonly LinkedList<DisassemblyTile> _tileChain;
    private readonly Dictionary<uint, DisassemblyTile> _addressToTileMap;
    private readonly LruCache<uint, DisassemblyTile> _tileCache;
    private readonly DisassemblyService? _disassemblyService;

    // Tile generation parameters
    private const int INSTRUCTIONS_PER_TILE = 50;
    private const int TILE_WIDTH = 800;
    private const int LINE_HEIGHT = 18;
    private const int MAX_CACHED_TILES = 20;

    // UI styling constants
    private const string FONT_FAMILY = "Consolas";
    private const double FONT_SIZE = 12.0;

    // Cached brushes for performance
    private readonly SolidColorBrush _backgroundBrush = new(Microsoft.UI.Colors.Black);
    private readonly SolidColorBrush _addressBrush = new(Microsoft.UI.Colors.Yellow);
    private readonly SolidColorBrush _symbolBrush = new(Microsoft.UI.Colors.Cyan);
    private readonly SolidColorBrush _instructionBrush = new(Microsoft.UI.Colors.LightGray);
    private readonly SolidColorBrush _textBrush = new(Microsoft.UI.Colors.White);

    public DisassemblyTileManager(DisassemblyService? disassemblyService = null)
    {
        _tileChain = new LinkedList<DisassemblyTile>();
        _addressToTileMap = new Dictionary<uint, DisassemblyTile>();
        _tileCache = new LruCache<uint, DisassemblyTile>(MAX_CACHED_TILES);
        _disassemblyService = disassemblyService;
    }

    /// <summary>
    /// Phase 3: Creates a visual tile preview with full feature support
    /// Includes breakpoint indicators, current instruction highlighting, and proper visual styling
    /// </summary>
    public FrameworkElement CreateTilePreview(List<LstEntry> entries, int width, int lineHeight, 
        IEnumerable<DisassemblyLineViewModel>? dataSource = null)
    {
        if (!entries.Any())
        {
            return CreateEmptyTilePreview(width, lineHeight);
        }

        var stackPanel = new StackPanel
        {
            Background = _backgroundBrush,
            Width = width,
            Orientation = Orientation.Vertical
        };

        foreach (var entry in entries)
        {
            // Find corresponding DisassemblyLineViewModel to get state information
            var viewModel = FindDisassemblyLineViewModel(entry.Address, dataSource);

            var lineGrid = new Grid
            {
                Height = lineHeight,
                Margin = new Thickness(0, 0, 0, 1), // Small gap between lines
                Background = GetLineBackground(viewModel),
                Tag = entry.Address // Store address for highlighting updates
            };

            lineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });  // Indicators
            lineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // Address
            lineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // Symbol
            lineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });     // Instruction

            // Indicator column (breakpoint and current instruction)
            var indicatorPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Breakpoint indicator (red circle)
            if (viewModel?.HasBreakpoint == true)
            {
                var breakpointIndicator = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(Microsoft.UI.Colors.Red),
                    Margin = new Thickness(0, 0, 2, 0)
                };
                indicatorPanel.Children.Add(breakpointIndicator);
            }

            // Current instruction indicator (arrow)
            if (viewModel?.IsCurrentInstruction == true)
            {
                var arrow = new TextBlock
                {
                    Text = "►",
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Yellow),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                };
                indicatorPanel.Children.Add(arrow);
            }

            Grid.SetColumn(indicatorPanel, 0);
            lineGrid.Children.Add(indicatorPanel);

            // Address column
            var addressText = new TextBlock
            {
                Text = $"0x{entry.Address:X8}",
                Foreground = _addressBrush,
                FontFamily = new FontFamily(FONT_FAMILY),
                FontSize = FONT_SIZE,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 8, 0)
            };
            Grid.SetColumn(addressText, 1);
            lineGrid.Children.Add(addressText);

            // Symbol column (if present)
            if (!string.IsNullOrEmpty(entry.SymbolName))
            {
                var symbolText = new TextBlock
                {
                    Text = entry.SymbolName,
                    Foreground = _symbolBrush,
                    FontFamily = new FontFamily(FONT_FAMILY),
                    FontSize = FONT_SIZE,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(symbolText, 2);
                lineGrid.Children.Add(symbolText);
            }

            // Instruction column
            var instructionText = new TextBlock
            {
                Text = entry.Instruction ?? "<no instruction>",
                Foreground = _instructionBrush,
                FontFamily = new FontFamily(FONT_FAMILY),
                FontSize = FONT_SIZE,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(instructionText, 3);
            lineGrid.Children.Add(instructionText);

            stackPanel.Children.Add(lineGrid);
        }

        // Wrap in a border for visual clarity
        var border = new Border
        {
            Child = stackPanel,
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.DarkGray),
            BorderThickness = new Thickness(1),
            Background = _backgroundBrush
        };

        return border;
    }

    /// <summary>
    /// Creates an empty tile preview for cases where no entries are available
    /// </summary>
    private FrameworkElement CreateEmptyTilePreview(int width, int lineHeight)
    {
        var textBlock = new TextBlock
        {
            Text = "No disassembly data available",
            Foreground = _textBrush,
            FontFamily = new FontFamily(FONT_FAMILY),
            FontSize = FONT_SIZE,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10)
        };

        var border = new Border
        {
            Child = textBlock,
            Width = width,
            Height = lineHeight * 3,
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.DarkGray),
            BorderThickness = new Thickness(1),
            Background = _backgroundBrush
        };

        return border;
    }

    /// <summary>
    /// Finds the tile containing the specified address
    /// </summary>
    public DisassemblyTile? FindTileContaining(uint address)
    {
        if (_addressToTileMap.TryGetValue(address, out var tile))
        {
            tile.Touch();
            return tile;
        }
        return null;
    }

    /// <summary>
    /// Gets or generates a tile containing the specified address
    /// </summary>
    public async Task<DisassemblyTile?> GetOrGenerateTileAsync(uint address, 
        IEnumerable<DisassemblyLineViewModel>? dataSource = null)
    {
        // Check if we already have a tile containing this address
        var existingTile = FindTileContaining(address);
        if (existingTile != null)
        {
            return existingTile;
        }

        // Generate a new tile
        return await GenerateTileAroundAddressAsync(address, dataSource, true);
    }

    /// <summary>
    /// Phase 3: Generates a new tile centered around the specified address with enhanced data integration
    /// </summary>
    public async Task<DisassemblyTile?> GenerateTileAroundAddressAsync(uint centerAddress, 
        IEnumerable<DisassemblyLineViewModel>? dataSource = null, bool andNeighbors = false)
    {
        if (_disassemblyService == null)
            return null;

        try
        {
            // Get entries around the center address
            var entries = _disassemblyService.GetEntriesAroundAddress(centerAddress, INSTRUCTIONS_PER_TILE);
            if (!entries.Any())
                return null;

            var tile = new DisassemblyTile
            {
                StartAddress = entries.First().Address,
                EndAddress = entries.Last().Address,
                SourceEntries = entries.ToList()
            };

            // Phase 3: Create visual tile with full feature support
            var tileElement = CreateTilePreview(entries.ToList(), TILE_WIDTH, LINE_HEIGHT, dataSource);

            // Store the FrameworkElement directly
            tile.TileElement = tileElement;
            tile.TileHeight = entries.Count * LINE_HEIGHT;

            // Calculate Y coordinates for each address
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var yCoordinate = i * LINE_HEIGHT + (LINE_HEIGHT / 2.0); // Center of the line
                tile.AddressYCoordinates[entry.Address] = yCoordinate;
            }

            // Add to cache and indexing structures
            AddTileToCache(tile);

            // Phase 3: Preload adjacent tiles for smooth scrolling
            if (andNeighbors)
            {
                await Task.Run(() => PreloadAdjacentTilesAsync(centerAddress, dataSource));
            }

            return tile;
        }
        catch (Exception ex)
        {
            // Log error in real implementation
            System.Diagnostics.Debug.WriteLine($"Error generating tile: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Phase 3: Preloads adjacent tiles in the background for smoother scrolling
    /// </summary>
    private async Task PreloadAdjacentTilesAsync(uint centerAddress, IEnumerable<DisassemblyLineViewModel>? dataSource = null)
    {
        if (_disassemblyService == null)
            return;

        try
        {
            // Calculate addresses for tiles before and after the current one
            var tileSize = (uint)(INSTRUCTIONS_PER_TILE * 4); // Estimate 4 bytes per instruction
            var prevTileAddress = centerAddress > tileSize ? centerAddress - tileSize : 0u;
            var nextTileAddress = centerAddress + tileSize;

            // Preload previous tile
            if (prevTileAddress > 0 && FindTileContaining(prevTileAddress) == null)
            {
                await GenerateTileAroundAddressAsync(prevTileAddress, dataSource, false);
            }

            // Preload next tile
            if (FindTileContaining(nextTileAddress) == null)
            {
                await GenerateTileAroundAddressAsync(nextTileAddress, dataSource, false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error preloading adjacent tiles: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a tile to the cache and indexing structures
    /// </summary>
    private void AddTileToCache(DisassemblyTile tile)
    {
        // Add to LRU cache
        _tileCache.Set(tile.StartAddress, tile);

        // Index all addresses in this tile for fast lookup
        foreach (var address in tile.AddressYCoordinates.Keys)
        {
            _addressToTileMap[address] = tile;
        }

        // Add to tile chain for sequential access
        _tileChain.AddLast(tile);
    }

    /// <summary>
    /// Removes a tile from cache and indexing structures
    /// </summary>
    private void RemoveTileFromCache(DisassemblyTile tile)
    {
        // Remove from LRU cache
        _tileCache.Remove(tile.StartAddress);

        // Remove all address mappings for this tile
        foreach (var address in tile.AddressYCoordinates.Keys)
        {
            _addressToTileMap.Remove(address);
        }

        // Remove from tile chain
        _tileChain.Remove(tile);

        // Dispose resources if needed
        tile.TileImage?.Dispose();
    }

    /// <summary>
    /// Phase 3: Determines if a tile is within the visible range (enhanced viewport checking)
    /// </summary>
    public bool IsInVisibleRange(DisassemblyTile tile, uint viewportStartAddress = 0, uint viewportEndAddress = uint.MaxValue)
    {
        // Check if tile overlaps with viewport range
        return tile.StartAddress <= viewportEndAddress && tile.EndAddress >= viewportStartAddress;
    }

    /// <summary>
    /// Clears all cached tiles
    /// </summary>
    public void ClearCache()
    {
        foreach (var tile in _tileChain.ToList())
        {
            RemoveTileFromCache(tile);
        }
    }

    /// <summary>
    /// Gets cache statistics for debugging
    /// </summary>
    public (int CachedTiles, int IndexedAddresses) GetCacheStats()
    {
        return (_tileCache.Count, _addressToTileMap.Count);
    }

    /// <summary>
    /// Phase 3: Helper method to find the corresponding DisassemblyLineViewModel for an address
    /// This allows us to access breakpoint and current instruction information
    /// </summary>
    private DisassemblyLineViewModel? FindDisassemblyLineViewModel(uint address, IEnumerable<DisassemblyLineViewModel>? dataSource)
    {
        if (dataSource == null)
            return null;

        return dataSource.FirstOrDefault(vm => 
        {
            var addressString = vm.Address.Replace("0x", "").Replace("0X", "");
            if (uint.TryParse(addressString, System.Globalization.NumberStyles.HexNumber, null, out uint vmAddress))
            {
                return vmAddress == address;
            }
            return false;
        });
    }

    /// <summary>
    /// Phase 3: Gets the background color for a line based on its state
    /// </summary>
    private Brush GetLineBackground(DisassemblyLineViewModel? viewModel)
    {
        if (viewModel == null)
            return _backgroundBrush;

        if (viewModel.IsCurrentInstruction)
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 40, 80)); // Dark blue for current instruction

        if (viewModel.HasBreakpoint)
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 40, 0, 0)); // Dark red for breakpoint

        return _backgroundBrush;
    }
}
