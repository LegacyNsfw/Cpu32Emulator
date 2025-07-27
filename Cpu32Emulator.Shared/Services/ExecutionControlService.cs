using System;
using System.Threading.Tasks;
using Cpu32Emulator.Models;

namespace Cpu32Emulator.Services
{
    /// <summary>
    /// Service for controlling CPU execution (stepping, running, breakpoints)
    /// </summary>
    public class ExecutionControlService
    {
        private readonly UnicornEmulatorService _emulator;
        private readonly MemoryManagerService _memoryManager;
        private readonly DisassemblyService _disassembly;
        private readonly BreakpointService _breakpoints;
        
        private ExecutionState _state = ExecutionState.Stopped;
        private uint _lastExecutedAddress;
        private int _instructionCount;
        private readonly object _stateLock = new object();

        /// <summary>
        /// Event raised when execution state changes
        /// </summary>
        public event EventHandler<ExecutionStateChangedEventArgs>? ExecutionStateChanged;

        /// <summary>
        /// Event raised when an instruction is executed
        /// </summary>
        public event EventHandler<InstructionExecutedEventArgs>? InstructionExecuted;

        /// <summary>
        /// Event raised when an exception occurs during execution
        /// </summary>
        public event EventHandler<ExecutionExceptionEventArgs>? ExecutionException;

        /// <summary>
        /// Gets the current execution state
        /// </summary>
        public ExecutionState State
        {
            get { lock (_stateLock) { return _state; } }
            private set 
            { 
                lock (_stateLock) 
                { 
                    if (_state != value)
                    {
                        var oldState = _state;
                        _state = value;
                        OnExecutionStateChanged(new ExecutionStateChangedEventArgs(oldState, value));
                    }
                } 
            }
        }

        /// <summary>
        /// Gets the address of the last executed instruction
        /// </summary>
        public uint LastExecutedAddress => _lastExecutedAddress;

        /// <summary>
        /// Gets the total number of instructions executed since reset
        /// </summary>
        public int InstructionCount => _instructionCount;

        /// <summary>
        /// Gets whether execution is currently running
        /// </summary>
        public bool IsRunning => State == ExecutionState.Running;

        /// <summary>
        /// Gets whether execution is currently stopped
        /// </summary>
        public bool IsStopped => State == ExecutionState.Stopped;

        /// <summary>
        /// Gets whether execution is paused
        /// </summary>
        public bool IsPaused => State == ExecutionState.Paused;

        public ExecutionControlService(
            UnicornEmulatorService emulator,
            MemoryManagerService memoryManager,
            DisassemblyService disassembly,
            BreakpointService breakpoints)
        {
            _emulator = emulator ?? throw new ArgumentNullException(nameof(emulator));
            _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
            _disassembly = disassembly ?? throw new ArgumentNullException(nameof(disassembly));
            _breakpoints = breakpoints ?? throw new ArgumentNullException(nameof(breakpoints));
        }

        /// <summary>
        /// Initializes the execution control service
        /// </summary>
        public void Initialize()
        {
            if (!_emulator.IsInitialized)
            {
                throw new InvalidOperationException("Emulator must be initialized first");
            }

            Reset();
        }

        /// <summary>
        /// Resets the execution state
        /// </summary>
        public void Reset()
        {
            lock (_stateLock)
            {
                _emulator.Reset();
                _lastExecutedAddress = 0;
                _instructionCount = 0;
                State = ExecutionState.Stopped;
            }
        }

        /// <summary>
        /// Executes a single instruction (F11 - Step Into)
        /// </summary>
        public async Task<StepResult> StepIntoAsync()
        {
            return await Task.Run(() => StepInto());
        }

        /// <summary>
        /// Executes a single instruction (F11 - Step Into) - synchronous version
        /// </summary>
        public StepResult StepInto()
        {
            if (!_emulator.IsInitialized)
                throw new InvalidOperationException("Emulator not initialized");

            try
            {
                State = ExecutionState.Running;

                // Get current PC before execution
                var cpuState = _emulator.GetCpuState();
                var currentPC = cpuState.PC;
                _lastExecutedAddress = currentPC;

                // Find the instruction at this address
                var lstEntry = _disassembly.FindEntryByAddress(currentPC);
                var instruction = lstEntry?.Instruction ?? "Unknown";

                // Execute the instruction
                _emulator.StepInstruction();
                _instructionCount++;

                // Get new CPU state
                var newCpuState = _emulator.GetCpuState();
                var newPC = newCpuState.PC;

                State = ExecutionState.Paused;

                var result = new StepResult
                {
                    Success = true,
                    ExecutedAddress = currentPC,
                    NextAddress = newPC,
                    Instruction = instruction,
                    CpuState = newCpuState
                };

                OnInstructionExecuted(new InstructionExecutedEventArgs(result));
                return result;
            }
            catch (Exception ex)
            {
                State = ExecutionState.Error;
                var errorResult = new StepResult
                {
                    Success = false,
                    ExecutedAddress = _lastExecutedAddress,
                    NextAddress = _lastExecutedAddress,
                    Instruction = "ERROR",
                    Exception = ex,
                    CpuState = null
                };

                OnExecutionException(new ExecutionExceptionEventArgs(ex, _lastExecutedAddress));
                return errorResult;
            }
        }

        /// <summary>
        /// Steps over the current instruction (F10 - Step Over)
        /// If it's a JSR, execution stops after the JSR returns
        /// </summary>
        public async Task<StepResult> StepOverAsync()
        {
            return await Task.Run(() => StepOver());
        }

        /// <summary>
        /// Steps over the current instruction (F10 - Step Over) - synchronous version
        /// </summary>
        public StepResult StepOver()
        {
            if (!_emulator.IsInitialized)
                throw new InvalidOperationException("Emulator not initialized");

            try
            {
                State = ExecutionState.Running;

                // Get current PC and instruction
                var cpuState = _emulator.GetCpuState();
                var currentPC = cpuState.PC;
                var lstEntry = _disassembly.FindEntryByAddress(currentPC);
                var instruction = lstEntry?.Instruction?.Trim().ToUpperInvariant() ?? "";

                // Check if this is a JSR (Jump to Subroutine) instruction
                if (instruction.StartsWith("JSR"))
                {
                    // For JSR, we need to step over the subroutine call
                    return StepOverSubroutine(currentPC);
                }
                else
                {
                    // For other instructions, just step into
                    return StepInto();
                }
            }
            catch (Exception ex)
            {
                State = ExecutionState.Error;
                var errorResult = new StepResult
                {
                    Success = false,
                    ExecutedAddress = _lastExecutedAddress,
                    NextAddress = _lastExecutedAddress,
                    Instruction = "ERROR",
                    Exception = ex,
                    CpuState = null
                };

                OnExecutionException(new ExecutionExceptionEventArgs(ex, _lastExecutedAddress));
                return errorResult;
            }
        }

        /// <summary>
        /// Runs execution until a breakpoint or error (F5 - Run)
        /// </summary>
        public async Task<ExecutionResult> RunAsync(uint maxInstructions = 100000)
        {
            return await Task.Run(() => Run(maxInstructions));
        }

        /// <summary>
        /// Runs execution until a breakpoint or error (F5 - Run) - synchronous version
        /// </summary>
        public ExecutionResult Run(uint maxInstructions = 100000)
        {
            if (!_emulator.IsInitialized)
                throw new InvalidOperationException("Emulator not initialized");

            try
            {
                State = ExecutionState.Running;

                var startPC = _emulator.GetCpuState().PC;
                var startInstructionCount = _instructionCount;
                var instructionsExecuted = 0;

                // Execute instructions one by one to check for breakpoints
                while (instructionsExecuted < maxInstructions)
                {
                    var currentState = _emulator.GetCpuState();
                    var currentPC = currentState.PC;

                    // Check for breakpoint at current PC
                    var breakpoint = _breakpoints.ShouldBreakAt(currentPC, currentState);
                    if (breakpoint != null)
                    {
                        State = ExecutionState.Paused;
                        return new ExecutionResult
                        {
                            Success = true,
                            StartAddress = startPC,
                            EndAddress = currentPC,
                            InstructionsExecuted = instructionsExecuted,
                            StopReason = ExecutionStopReason.Breakpoint,
                            FinalCpuState = currentState,
                            BreakpointHit = breakpoint
                        };
                    }

                    // Execute one instruction
                    _emulator.StepInstruction();
                    _instructionCount++;
                    instructionsExecuted++;
                    _lastExecutedAddress = currentPC;
                }

                // Reached max instructions
                var endPC = _emulator.GetCpuState().PC;
                var endCpuState = _emulator.GetCpuState();

                State = ExecutionState.Paused;

                return new ExecutionResult
                {
                    Success = true,
                    StartAddress = startPC,
                    EndAddress = endPC,
                    InstructionsExecuted = instructionsExecuted,
                    StopReason = ExecutionStopReason.MaxInstructionsReached,
                    FinalCpuState = endCpuState
                };
            }
            catch (Exception ex)
            {
                State = ExecutionState.Error;
                OnExecutionException(new ExecutionExceptionEventArgs(ex, _lastExecutedAddress));

                return new ExecutionResult
                {
                    Success = false,
                    StartAddress = _lastExecutedAddress,
                    EndAddress = _lastExecutedAddress,
                    InstructionsExecuted = 0,
                    StopReason = ExecutionStopReason.Exception,
                    Exception = ex,
                    FinalCpuState = null
                };
            }
        }

        /// <summary>
        /// Stops execution
        /// </summary>
        public void Stop()
        {
            State = ExecutionState.Stopped;
        }

        /// <summary>
        /// Pauses execution
        /// </summary>
        public void Pause()
        {
            if (State == ExecutionState.Running)
            {
                State = ExecutionState.Paused;
            }
        }

        /// <summary>
        /// Sets the program counter to a specific address
        /// </summary>
        public void SetProgramCounter(uint address)
        {
            if (!_emulator.IsInitialized)
                throw new InvalidOperationException("Emulator not initialized");

            if (!_memoryManager.IsAddressMapped(address))
                throw new InvalidOperationException($"Address 0x{address:X8} is not mapped");

            var cpuState = _emulator.GetCpuState();
            cpuState.PC = address;
            _emulator.SetCpuState(cpuState);
            _lastExecutedAddress = address;
        }

        /// <summary>
        /// Steps over a subroutine call
        /// </summary>
        private StepResult StepOverSubroutine(uint jsrAddress)
        {
            // Get the address of the instruction after the JSR
            // This is a simplified implementation - in reality, we'd need to
            // parse the JSR instruction to determine its size
            var nextInstructionAddress = jsrAddress + 4; // Assume 4-byte JSR for now

            // Execute the JSR
            var result = StepInto();
            if (!result.Success)
                return result;

            // Continue execution until we return to the next instruction
            var maxSteps = 10000; // Prevent infinite loops
            var steps = 0;

            while (steps < maxSteps)
            {
                var currentState = _emulator.GetCpuState();
                if (currentState.PC == nextInstructionAddress)
                {
                    // We've returned from the subroutine
                    State = ExecutionState.Paused;
                    result.NextAddress = currentState.PC;
                    result.CpuState = currentState;
                    return result;
                }

                // Step one more instruction
                var stepResult = StepInto();
                if (!stepResult.Success)
                    return stepResult;

                steps++;
            }

            // If we get here, we hit the max steps limit
            throw new InvalidOperationException("Step over exceeded maximum instruction limit - possible infinite loop");
        }

        /// <summary>
        /// Gets the current CPU state
        /// </summary>
        public CpuState GetCurrentCpuState()
        {
            if (!_emulator.IsInitialized)
                throw new InvalidOperationException("Emulator not initialized");

            return _emulator.GetCpuState();
        }

        /// <summary>
        /// Sets the CPU state
        /// </summary>
        public void SetCpuState(CpuState state)
        {
            if (!_emulator.IsInitialized)
                throw new InvalidOperationException("Emulator not initialized");

            _emulator.SetCpuState(state);
        }

        /// <summary>
        /// Raises the ExecutionStateChanged event
        /// </summary>
        protected virtual void OnExecutionStateChanged(ExecutionStateChangedEventArgs e)
        {
            ExecutionStateChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the InstructionExecuted event
        /// </summary>
        protected virtual void OnInstructionExecuted(InstructionExecutedEventArgs e)
        {
            InstructionExecuted?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the ExecutionException event
        /// </summary>
        protected virtual void OnExecutionException(ExecutionExceptionEventArgs e)
        {
            ExecutionException?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Execution states
    /// </summary>
    public enum ExecutionState
    {
        Stopped,
        Running,
        Paused,
        Error
    }

    /// <summary>
    /// Result of a single step operation
    /// </summary>
    public class StepResult
    {
        public bool Success { get; set; }
        public uint ExecutedAddress { get; set; }
        public uint NextAddress { get; set; }
        public string Instruction { get; set; } = string.Empty;
        public CpuState? CpuState { get; set; }
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Result of a run operation
    /// </summary>
    public class ExecutionResult
    {
        public bool Success { get; set; }
        public uint StartAddress { get; set; }
        public uint EndAddress { get; set; }
        public int InstructionsExecuted { get; set; }
        public ExecutionStopReason StopReason { get; set; }
        public CpuState? FinalCpuState { get; set; }
        public Exception? Exception { get; set; }
        public Breakpoint? BreakpointHit { get; set; }
    }

    /// <summary>
    /// Reasons why execution stopped
    /// </summary>
    public enum ExecutionStopReason
    {
        Breakpoint,
        MaxInstructionsReached,
        Exception,
        UserStopped
    }

    /// <summary>
    /// Event arguments for execution state changes
    /// </summary>
    public class ExecutionStateChangedEventArgs : EventArgs
    {
        public ExecutionState OldState { get; }
        public ExecutionState NewState { get; }

        public ExecutionStateChangedEventArgs(ExecutionState oldState, ExecutionState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }

    /// <summary>
    /// Event arguments for instruction executed events
    /// </summary>
    public class InstructionExecutedEventArgs : EventArgs
    {
        public StepResult Result { get; }

        public InstructionExecutedEventArgs(StepResult result)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }
    }

    /// <summary>
    /// Event arguments for execution exceptions
    /// </summary>
    public class ExecutionExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public uint Address { get; }

        public ExecutionExceptionEventArgs(Exception exception, uint address)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Address = address;
        }
    }
}
