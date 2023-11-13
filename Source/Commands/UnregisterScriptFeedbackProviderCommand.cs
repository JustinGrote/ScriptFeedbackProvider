using System.Management.Automation;
using System.Management.Automation.Subsystem.Feedback;
using static System.Management.Automation.Subsystem.SubsystemInfo;
using static System.Management.Automation.Subsystem.SubsystemManager;
using static System.Management.Automation.VerbsLifecycle;

namespace PSFeedbackProviderNS;

public interface SubsystemInfo
{
  public Guid Id { get; }
}

[Cmdlet(Unregister, "ScriptFeedbackProvider")]
public class UnRegisterScriptFeedbackProviderCommand : PSCmdlet
{
  [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
  public ImplementationInfo? Provider;

  protected override void ProcessRecord()
  {
    UnregisterSubsystem<IFeedbackProvider>(Provider!.Id);
  }
}
