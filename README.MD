# PowerShell Script Based Feedback Providers

This module allows you to write feedback providers in PowerShell rather than C# and register them.

By default, feedback providers are registered for errors only. To register for another type, use the `-Trigger` parameter.

Also errors and potential issues are silently suppressed by default. Use the `-ShowDebugInfo` parameter of `Register-ScriptFeedbackProvider` to get useful information for troubleshooting feedback providers.

## Usage
A `[FeedbackContext]` is provided as an argument and you must return a single `[FeedbackItem]` object.

You can access the feedback context either via `$args[0]` or as `param($context)`

You can return a `[FeedbackItem]` result multiple ways:

1. Return a FeedbackItem object. This is the safest method. `[FeedbackItem]::new('header',@('action1','action2'))`
1. Return a single string, that will fill in for the header.
1. Return multiple strings, the first will be the header and the others will be the defined actions
1. Return a hashtable with the same properties as the FeedbackItem class.

## Examples

### Echo the last command

```powershell
Register-ScriptFeedbackProvider -Name EchoCommand -Trigger All -ScriptBlock {
  param($context)
  [FeedbackItem]::new("Command was", $context.CommandLine)
}
```

![Alt text](images/README/image.png)

### Report on a recommended action

```powershell
Register-ScriptFeedbackProvider -Name 'Error Recommended Action' {
  param($context)
  if ($context.LastError.ErrorDetails.RecommendedAction) {
    [FeedbackItem]::new(
      'The last error has a recommended action:',
      $context.LastError.ErrorDetails.RecommendedAction
    )
  }
}
```

![Alt text](images/README/image-1.png)

### Using simplified string syntax

```powershell
Register-ScriptFeedbackProvider -Name 'Error Recommended Action' {
  param($context)
  if ($context.LastError.ErrorDetails.RecommendedAction) {
    'The last error has a recommended action:'
    $context.LastError.ErrorDetails.RecommendedAction
  }
}
```
![Alt text](images/README/image-2.png)

## Authoring Intellisense

To get intellisense for $context, add the namespace at the top of your file.

```powershell
using namespace System.Management.Automation.Subsystem.Feedback
{
  param([FeedbackContext]$context)
  $context.<tab>
}
```

But do not include the namespace in your final command
