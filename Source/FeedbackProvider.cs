using System.Management.Automation;
using System.Management.Automation.Subsystem.Feedback;
using static System.Management.Automation.Subsystem.SubsystemManager;
using static System.Management.Automation.Subsystem.SubsystemKind;
using System.Collections.ObjectModel;

namespace PSFeedbackProviderNS;

public class ScriptFeedbackProvider : IFeedbackProvider, IDisposable
{
  public ScriptFeedbackProvider(ScriptBlock scriptBlock, string? name, string? description, Guid? id, FeedbackTrigger? trigger)
  {
    Id = id ?? Guid.NewGuid();
    this.scriptBlock = scriptBlock;
    Name = name ?? $"PSFeedbackProvider";
    Description = description ?? "A feedback provider that runs a scriptblock";
    ps = PowerShell.Create();
    Trigger = trigger ?? FeedbackTrigger.Error;
  }
  public PowerShell ps { get; init; }
  public ScriptBlock scriptBlock { get; init; }
  public Guid Id { get; init; }
  public string Name { get; init; }
  public string Description { get; init; }

  #region IFeedbackProvider
  public FeedbackTrigger Trigger { get; }
  public FeedbackItem? GetFeedback(FeedbackContext context, CancellationToken token)
  {
    Collection<FeedbackItem>? results;
    try
    {
      ps.Commands.Clear();

      results = ps
        // Allows [FeedbackItem] to be used easily
        .AddScript("using namespace System.Management.Automation.Subsystem.Feedback")
        .AddScript(scriptBlock.ToString())
        .AddArgument(context)
        .Invoke<FeedbackItem>();
    }
    catch (Exception err)
    {
      Console.Error.WriteLine($"{Name} Exception: {err.Message}");
      return null;
    }

    if (results.Count < 1)
    {
      // TODO: surface this better
      Console.Error.WriteLine($"{Name} INFO: No feedback item was received");
      return null;
    }
    // TODO: Surface if multiple feedbackItems were received, as this is invalid

    return results[0];
  }
  #endregion IFeedbackProvider

  public void Register()
  {
    RegisterSubsystem(FeedbackProvider, this);
    // Warm up the runspace to minimize the first run penalty
    ps.AddScript("$null");
    ps.Invoke();
  }

  public void Dispose()
  {
    UnregisterSubsystem<ScriptFeedbackProvider>(Id);
  }
}
