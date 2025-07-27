using System;
using System.Collections.Generic;
using System.Linq;
using Cpu32Emulator.Models;

namespace Cpu32Emulator.Services
{
    /// <summary>
    /// Service for managing execution breakpoints
    /// </summary>
    public class BreakpointService
    {
        private readonly List<Breakpoint> _breakpoints = new();
        private readonly object _breakpointLock = new object();

        /// <summary>
        /// Event raised when a breakpoint is added
        /// </summary>
        public event EventHandler<BreakpointEventArgs>? BreakpointAdded;

        /// <summary>
        /// Event raised when a breakpoint is removed
        /// </summary>
        public event EventHandler<BreakpointEventArgs>? BreakpointRemoved;

        /// <summary>
        /// Event raised when a breakpoint is hit during execution
        /// </summary>
        public event EventHandler<BreakpointHitEventArgs>? BreakpointHit;

        /// <summary>
        /// Gets all breakpoints
        /// </summary>
        public IReadOnlyList<Breakpoint> Breakpoints
        {
            get
            {
                lock (_breakpointLock)
                {
                    return _breakpoints.ToList().AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Gets the number of breakpoints
        /// </summary>
        public int Count
        {
            get
            {
                lock (_breakpointLock)
                {
                    return _breakpoints.Count;
                }
            }
        }

        /// <summary>
        /// Adds a breakpoint at the specified address
        /// </summary>
        public Breakpoint AddBreakpoint(uint address, string? condition = null, string? description = null)
        {
            lock (_breakpointLock)
            {
                // Check if breakpoint already exists at this address
                var existing = _breakpoints.FirstOrDefault(bp => bp.Address == address);
                if (existing != null)
                {
                    return existing;
                }

                var breakpoint = new Breakpoint
                {
                    Id = Guid.NewGuid(),
                    Address = address,
                    Condition = condition,
                    Description = description,
                    IsEnabled = true,
                    HitCount = 0,
                    CreatedAt = DateTime.Now
                };

                _breakpoints.Add(breakpoint);
                OnBreakpointAdded(new BreakpointEventArgs(breakpoint));
                return breakpoint;
            }
        }

        /// <summary>
        /// Removes a breakpoint by ID
        /// </summary>
        public bool RemoveBreakpoint(Guid id)
        {
            lock (_breakpointLock)
            {
                var breakpoint = _breakpoints.FirstOrDefault(bp => bp.Id == id);
                if (breakpoint != null)
                {
                    _breakpoints.Remove(breakpoint);
                    OnBreakpointRemoved(new BreakpointEventArgs(breakpoint));
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Removes a breakpoint by address
        /// </summary>
        public bool RemoveBreakpoint(uint address)
        {
            lock (_breakpointLock)
            {
                var breakpoint = _breakpoints.FirstOrDefault(bp => bp.Address == address);
                if (breakpoint != null)
                {
                    _breakpoints.Remove(breakpoint);
                    OnBreakpointRemoved(new BreakpointEventArgs(breakpoint));
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Removes all breakpoints
        /// </summary>
        public void ClearAllBreakpoints()
        {
            lock (_breakpointLock)
            {
                var breakpointsToRemove = _breakpoints.ToList();
                _breakpoints.Clear();

                foreach (var breakpoint in breakpointsToRemove)
                {
                    OnBreakpointRemoved(new BreakpointEventArgs(breakpoint));
                }
            }
        }

        /// <summary>
        /// Gets a breakpoint by ID
        /// </summary>
        public Breakpoint? GetBreakpoint(Guid id)
        {
            lock (_breakpointLock)
            {
                return _breakpoints.FirstOrDefault(bp => bp.Id == id);
            }
        }

        /// <summary>
        /// Gets a breakpoint by address
        /// </summary>
        public Breakpoint? GetBreakpoint(uint address)
        {
            lock (_breakpointLock)
            {
                return _breakpoints.FirstOrDefault(bp => bp.Address == address);
            }
        }

        /// <summary>
        /// Enables or disables a breakpoint
        /// </summary>
        public bool SetBreakpointEnabled(Guid id, bool enabled)
        {
            lock (_breakpointLock)
            {
                var breakpoint = _breakpoints.FirstOrDefault(bp => bp.Id == id);
                if (breakpoint != null)
                {
                    breakpoint.IsEnabled = enabled;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Enables or disables a breakpoint by address
        /// </summary>
        public bool SetBreakpointEnabled(uint address, bool enabled)
        {
            lock (_breakpointLock)
            {
                var breakpoint = _breakpoints.FirstOrDefault(bp => bp.Address == address);
                if (breakpoint != null)
                {
                    breakpoint.IsEnabled = enabled;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Checks if there's an active breakpoint at the specified address
        /// </summary>
        public bool HasBreakpointAt(uint address)
        {
            lock (_breakpointLock)
            {
                return _breakpoints.Any(bp => bp.Address == address && bp.IsEnabled);
            }
        }

        /// <summary>
        /// Checks if execution should break at the specified address
        /// Returns the breakpoint if it should break, null otherwise
        /// </summary>
        public Breakpoint? ShouldBreakAt(uint address, CpuState? cpuState = null)
        {
            lock (_breakpointLock)
            {
                var breakpoint = _breakpoints.FirstOrDefault(bp => bp.Address == address && bp.IsEnabled);
                if (breakpoint == null)
                    return null;

                // Evaluate condition if present
                if (!string.IsNullOrEmpty(breakpoint.Condition))
                {
                    // TODO: Implement condition evaluation
                    // For now, simple conditions like "D0==5" could be parsed
                    // This would require a simple expression evaluator
                    if (!EvaluateCondition(breakpoint.Condition, cpuState))
                        return null;
                }

                // Increment hit count
                breakpoint.HitCount++;
                breakpoint.LastHitAt = DateTime.Now;

                OnBreakpointHit(new BreakpointHitEventArgs(breakpoint, cpuState));
                return breakpoint;
            }
        }

        /// <summary>
        /// Updates a breakpoint's properties
        /// </summary>
        public bool UpdateBreakpoint(Guid id, string? condition = null, string? description = null)
        {
            lock (_breakpointLock)
            {
                var breakpoint = _breakpoints.FirstOrDefault(bp => bp.Id == id);
                if (breakpoint != null)
                {
                    if (condition != null)
                        breakpoint.Condition = condition;
                    if (description != null)
                        breakpoint.Description = description;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Gets breakpoints in the specified address range
        /// </summary>
        public IList<Breakpoint> GetBreakpointsInRange(uint startAddress, uint endAddress)
        {
            lock (_breakpointLock)
            {
                return _breakpoints
                    .Where(bp => bp.Address >= startAddress && bp.Address <= endAddress)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets enabled breakpoints only
        /// </summary>
        public IList<Breakpoint> GetEnabledBreakpoints()
        {
            lock (_breakpointLock)
            {
                return _breakpoints.Where(bp => bp.IsEnabled).ToList();
            }
        }

        /// <summary>
        /// Resets hit counts for all breakpoints
        /// </summary>
        public void ResetHitCounts()
        {
            lock (_breakpointLock)
            {
                foreach (var breakpoint in _breakpoints)
                {
                    breakpoint.HitCount = 0;
                    breakpoint.LastHitAt = null;
                }
            }
        }

        /// <summary>
        /// Simple condition evaluator (basic implementation)
        /// </summary>
        private bool EvaluateCondition(string condition, CpuState? cpuState)
        {
            if (cpuState == null)
                return true;

            try
            {
                // Very simple condition evaluation
                // Supports: D0==value, D1!=value, A0==value, PC==value, etc.
                condition = condition.Trim().ToUpperInvariant();

                if (condition.Contains("=="))
                {
                    var parts = condition.Split(new[] { "==" }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var register = parts[0].Trim();
                        if (uint.TryParse(parts[1].Trim(), out uint value))
                        {
                            return GetRegisterValue(register, cpuState) == value;
                        }
                    }
                }
                else if (condition.Contains("!="))
                {
                    var parts = condition.Split(new[] { "!=" }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var register = parts[0].Trim();
                        if (uint.TryParse(parts[1].Trim(), out uint value))
                        {
                            return GetRegisterValue(register, cpuState) != value;
                        }
                    }
                }

                // If we can't evaluate, assume true
                return true;
            }
            catch
            {
                // If evaluation fails, assume true
                return true;
            }
        }

        /// <summary>
        /// Gets the value of a register by name
        /// </summary>
        private uint GetRegisterValue(string register, CpuState cpuState)
        {
            return register switch
            {
                "D0" => cpuState.GetDataRegister(0),
                "D1" => cpuState.GetDataRegister(1),
                "D2" => cpuState.GetDataRegister(2),
                "D3" => cpuState.GetDataRegister(3),
                "D4" => cpuState.GetDataRegister(4),
                "D5" => cpuState.GetDataRegister(5),
                "D6" => cpuState.GetDataRegister(6),
                "D7" => cpuState.GetDataRegister(7),
                "A0" => cpuState.GetAddressRegister(0),
                "A1" => cpuState.GetAddressRegister(1),
                "A2" => cpuState.GetAddressRegister(2),
                "A3" => cpuState.GetAddressRegister(3),
                "A4" => cpuState.GetAddressRegister(4),
                "A5" => cpuState.GetAddressRegister(5),
                "A6" => cpuState.GetAddressRegister(6),
                "PC" => cpuState.PC,
                "SR" => cpuState.SR,
                "USP" => cpuState.USP,
                "SSP" => cpuState.SSP,
                _ => 0
            };
        }

        /// <summary>
        /// Raises the BreakpointAdded event
        /// </summary>
        protected virtual void OnBreakpointAdded(BreakpointEventArgs e)
        {
            BreakpointAdded?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the BreakpointRemoved event
        /// </summary>
        protected virtual void OnBreakpointRemoved(BreakpointEventArgs e)
        {
            BreakpointRemoved?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the BreakpointHit event
        /// </summary>
        protected virtual void OnBreakpointHit(BreakpointHitEventArgs e)
        {
            BreakpointHit?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Represents a breakpoint
    /// </summary>
    public class Breakpoint
    {
        public Guid Id { get; set; }
        public uint Address { get; set; }
        public string? Condition { get; set; }
        public string? Description { get; set; }
        public bool IsEnabled { get; set; }
        public int HitCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastHitAt { get; set; }

        public override string ToString()
        {
            var desc = !string.IsNullOrEmpty(Description) ? $" - {Description}" : "";
            var cond = !string.IsNullOrEmpty(Condition) ? $" ({Condition})" : "";
            return $"0x{Address:X8}{desc}{cond} [{(IsEnabled ? "Enabled" : "Disabled")}, Hits: {HitCount}]";
        }
    }

    /// <summary>
    /// Event arguments for breakpoint events
    /// </summary>
    public class BreakpointEventArgs : EventArgs
    {
        public Breakpoint Breakpoint { get; }

        public BreakpointEventArgs(Breakpoint breakpoint)
        {
            Breakpoint = breakpoint ?? throw new ArgumentNullException(nameof(breakpoint));
        }
    }

    /// <summary>
    /// Event arguments for breakpoint hit events
    /// </summary>
    public class BreakpointHitEventArgs : EventArgs
    {
        public Breakpoint Breakpoint { get; }
        public CpuState? CpuState { get; }

        public BreakpointHitEventArgs(Breakpoint breakpoint, CpuState? cpuState)
        {
            Breakpoint = breakpoint ?? throw new ArgumentNullException(nameof(breakpoint));
            CpuState = cpuState;
        }
    }
}
