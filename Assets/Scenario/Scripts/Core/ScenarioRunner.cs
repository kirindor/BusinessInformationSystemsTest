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

    [SerializeField] private ScenarioLogger logger;
    [SerializeField] private string outOfTurnText = "Этот шаг ещё не начат:";
    [SerializeField] private string limitText = "Порог нарушений достигнут, оставшиеся шаги группы пропущены";

    public UnityEvent onStarted = new UnityEvent();
    public UnityEvent onChanged = new UnityEvent();
    public UnityEvent<string> onViolation = new UnityEvent<string>();
    public UnityEvent onFinished = new UnityEvent();

    private readonly Dictionary<ScenarioStep, StepState> states = new Dictionary<ScenarioStep, StepState>();
    private readonly List<ScenarioStep> activeSteps = new List<ScenarioStep>();
    private ScenarioMode mode;
    private int groupIndex;
    private int stageIndex;
    private int groupViolations;
    private float startTime;

    public bool IsRunning { get; private set; }
    public ScenarioMode Mode => mode;
    public ScenarioLogger Logger => logger;
    public IReadOnlyList<ScenarioStep> ActiveSteps => activeSteps;
    public StepGroup CurrentGroup => mode.groups[groupIndex];
    public int GroupIndex => groupIndex;
    public int GroupViolations => groupViolations;

    public int GetAttempts(ScenarioStep step) => states[step].attempts;

    public void Run(ScenarioMode newMode)
    {
        Cleanup();
        mode = newMode;
        startTime = Time.time;
        IsRunning = true;
        logger.BeginRun();
        Write(LogEvents.RunStarted, null, null, mode.title);

        foreach (var group in mode.groups)
        {
            foreach (var stage in group.stages)
            {
                foreach (var step in stage.steps)
                {
                    if (states.ContainsKey(step))
                    {
                        Debug.LogError($"Шаг {step.name} добавлен в режим «{mode.title}» несколько раз", step);
                        continue;
                    }

                    states.Add(step, new StepState { group = group });
                    step.Reported += OnStepReported;
                    Write(LogEvents.StepPlanned, step, null, step.Description);
                }
            }
        }

        onStarted.Invoke();
        groupIndex = 0;
        StartGroup();
        onChanged.Invoke();
    }

    public void Restart()
    {
        Run(mode);
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
        var state = states[step];
        if (state.status != StepStatus.Pending && state.status != StepStatus.Active)
            return;

        if (state.status == StepStatus.Pending)
        {
            if (mode.outOfTurn == OutOfTurnAction.Ignore)
            {
                Write(LogEvents.StepAction, step, LogEvents.Ignored);
                return;
            }

            success = false;
            reason = $"{outOfTurnText} «{step.Description}»";
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
            if (!mode.retryAfterViolation)
                Close(step, StepStatus.Failed);

            groupViolations++;
            if (mode.violationLimit > 0 && groupViolations >= mode.violationLimit)
            {
                SkipCurrentGroup();
                reason += "\n" + limitText;
            }

            onViolation.Invoke(reason);
        }

        if (activeSteps.Count == 0)
            NextStage();

        onChanged.Invoke();
    }

    private void StartGroup()
    {
        groupViolations = 0;
        stageIndex = -1;
        Write(LogEvents.GroupStarted, null, null, CurrentGroup.name);
        NextStage();
    }

    private void NextStage()
    {
        var stages = CurrentGroup.stages;
        while (++stageIndex < stages.Count)
        {
            foreach (var step in stages[stageIndex].steps)
            {
                if (states[step].status == StepStatus.Pending)
                    Activate(step);
            }

            if (activeSteps.Count > 0)
                return;
        }

        if (++groupIndex < mode.groups.Count)
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
                var status = states[step].status;
                if (status == StepStatus.Pending || status == StepStatus.Active)
                    Close(step, StepStatus.Skipped);
            }
        }
    }

    private void Activate(ScenarioStep step)
    {
        states[step].status = StepStatus.Active;
        activeSteps.Add(step);
        Write(LogEvents.StepActivated, step);
        step.onActivated.Invoke();
    }

    private void Close(ScenarioStep step, StepStatus status)
    {
        var state = states[step];
        var wasActive = state.status == StepStatus.Active;
        state.status = status;
        Write(LogEvents.StepFinished, step, status.ToString());

        if (wasActive)
        {
            activeSteps.Remove(step);
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
        foreach (var step in activeSteps)
            step.onDeactivated.Invoke();

        foreach (var step in states.Keys)
            step.Reported -= OnStepReported;

        activeSteps.Clear();
        states.Clear();
        IsRunning = false;
    }

    private void Write(string type, ScenarioStep step = null, string result = null, string message = null)
    {
        logger.Write(new LogEntry
        {
            type = type,
            time = Time.time - startTime,
            stepId = step != null ? step.name : null,
            group = step != null ? states[step].group.name : null,
            result = result,
            attempt = step != null ? states[step].attempts : 0,
            message = message
        });
    }
}
