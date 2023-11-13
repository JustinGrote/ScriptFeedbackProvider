using System.Management.Automation;
using System.Management.Automation.Subsystem.Feedback;
using static System.Management.Automation.VerbsLifecycle;

namespace PSFeedbackProviderNS;

[Cmdlet(Register, "ScriptFeedbackProvider")]
public class RegisterScriptFeedbackProviderCommand : PSCmdlet
{
  [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
  /// <summary>
  /// A scriptblock that takes a FeedbackContext as a parameter and returns a FeedbackItem.
  /// </summary>
  public ScriptBlock? ScriptBlock;

  [Parameter(Position = 1)]
  public FeedbackTrigger? Trigger;

  [Parameter(Position = 2)]
  public string? Name;

  [Parameter(Position = 3)]
  public string? Description;

  [Parameter(Position = 4)]
  public Guid? Guid;

  protected override void ProcessRecord()
  {
    var provider = new ScriptFeedbackProvider(ScriptBlock!, Name, Description, Guid, Trigger);
    provider.Register();
  }
}
