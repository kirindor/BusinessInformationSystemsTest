using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ScenarioRunner : MonoBehaviour
{
    private class StepState
    {
        public StepStatus status;
        public int attempts;
        public StepGroup group;
    }

    [SerializeField] private ScenarioLogger _logger;
    [SerializeField] private string _outOfTurnText = "Этот шаг ещё не начат:";
    [SerializeField] private string _limitText = "Порог нарушений достигнут, оставшиеся шаги группы пропущены";

    public UnityEvent onStarted = new UnityEvent();
    public UnityEvent onChanged = new UnityEvent();
    public UnityEvent<string> onViolation = new UnityEvent<string>();
    public UnityEvent onFinished = new UnityEvent();

    private readonly Dictionary<ScenarioStep, StepState> _states = new Dictionary<ScenarioStep, StepState>();
    private readonly List<ScenarioStep> _activeSteps = new List<ScenarioStep>();
    private ScenarioMode _mode;
    private int _groupIndex;
    private int _stageIndex;
    private int _groupViolations;
    private float _startTime;

    public bool IsRunning { get; private set; }
    public ScenarioMode Mode => _mode;
    public ScenarioLogger Logger => _logger;
    public IReadOnlyList<ScenarioStep> ActiveSteps => _activeSteps;
    public StepGroup CurrentGroup => _mode.groups[_groupIndex];
    public int GroupIndex => _groupIndex;
    public int GroupViolations => _groupViolations;

    public int GetAttempts(ScenarioStep step) => _states[step].attempts;

    public void Run(ScenarioMode mode)
    {
        Cleanup();
        _mode = mode;
        _startTime = Time.time;
        IsRunning = true;
        _logger.BeginRun();
        Write(LogEvents.RunStarted, null, null, _mode.title);

        foreach (var group in _mode.groups)
        {
            foreach (var stage in group.stages)
            {
                foreach (var step in stage.steps)
                {
                    if (_states.ContainsKey(step))
                    {
                        Debug.LogError($"Шаг {step.name} добавлен в режим «{_mode.title}» несколько раз", step);
                        continue;
                    }

                    _states.Add(step, new StepState { group = group });
                    step.Reported += OnStepReported;
                    Write(LogEvents.StepPlanned, step, null, step.Description);
                }
            }
        }

        onStarted.Invoke();
        _groupIndex = 0;
        StartGroup();
        onChanged.Invoke();
    }

    public void Restart()
    {
        Run(_mode);
    }

    public void Abort()
    {
        if (!IsRunning)
            return;

        Write(LogEvents.RunAborted);
        Cleanup();
        onChanged.Invoke();
        onFinished.Invoke();
    }

    private void OnStepReported(ScenarioStep step, bool success, string reason)
    {
        var state = _states[step];
        if (state.status != StepStatus.Pending && state.status != StepStatus.Active)
            return;

        if (state.status == StepStatus.Pending)
        {
            if (_mode.outOfTurn == OutOfTurnAction.Ignore)
            {
                Write(LogEvents.StepAction, step, LogEvents.Ignored);
                return;
            }

            success = false;
            reason = $"{_outOfTurnText} «{step.Description}»";
        }

        state.attempts++;

        if (success)
        {
            Write(LogEvents.StepAction, step, LogEvents.Success);
            Close(step, StepStatus.Completed);
        }
        else
        {
            Write(LogEvents.StepAction, step, LogEvents.Violation, reason);
            if (!_mode.retryAfterViolation)
                Close(step, StepStatus.Failed);

            _groupViolations++;
            if (_mode.violationLimit > 0 && _groupViolations >= _mode.violationLimit)
            {
                SkipCurrentGroup();
                reason += "\n" + _limitText;
            }

            onViolation.Invoke(reason);
        }

        if (_activeSteps.Count == 0)
            NextStage();

        onChanged.Invoke();
    }

    private void StartGroup()
    {
        _groupViolations = 0;
        _stageIndex = -1;
        Write(LogEvents.GroupStarted, null, null, CurrentGroup.name);
        NextStage();
    }

    private void NextStage()
    {
        var stages = CurrentGroup.stages;
        while (++_stageIndex < stages.Count)
        {
            foreach (var step in stages[_stageIndex].steps)
            {
                if (_states[step].status == StepStatus.Pending)
                    Activate(step);
            }

            if (_activeSteps.Count > 0)
                return;
        }

        if (++_groupIndex < _mode.groups.Count)
            StartGroup();
        else
            Finish();
    }

    private void SkipCurrentGroup()
    {
        foreach (var stage in CurrentGroup.stages)
        {
            foreach (var step in stage.steps)
            {
                var status = _states[step].status;
                if (status == StepStatus.Pending || status == StepStatus.Active)
                    Close(step, StepStatus.Skipped);
            }
        }
    }

    private void Activate(ScenarioStep step)
    {
        _states[step].status = StepStatus.Active;
        _activeSteps.Add(step);
        Write(LogEvents.StepActivated, step);
        step.onActivated.Invoke();
    }

    private void Close(ScenarioStep step, StepStatus status)
    {
        var state = _states[step];
        var wasActive = state.status == StepStatus.Active;
        state.status = status;
        Write(LogEvents.StepFinished, step, status.ToString());

        if (wasActive)
        {
            _activeSteps.Remove(step);
            step.onDeactivated.Invoke();
        }
    }

    private void Finish()
    {
        Write(LogEvents.RunFinished);
        Cleanup();
        onFinished.Invoke();
    }

    private void Cleanup()
    {
        foreach (var step in _activeSteps)
            step.onDeactivated.Invoke();

        foreach (var step in _states.Keys)
            step.Reported -= OnStepReported;

        _activeSteps.Clear();
        _states.Clear();
        IsRunning = false;
    }

    private void Write(string type, ScenarioStep step = null, string result = null, string message = null)
    {
        _logger.Write(new LogEntry
        {
            type = type,
            time = Time.time - _startTime,
            stepId = step != null ? step.name : null,
            group = step != null ? _states[step].group.name : null,
            result = result,
            attempt = step != null ? _states[step].attempts : 0,
            message = message
        });
    }
}
