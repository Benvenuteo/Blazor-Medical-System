using Microsoft.AspNetCore.Components;

namespace MedicalBookingSystem.BlazorServer.Shared;

/// <summary>Keep prerendered forms inert until their first interactive render, so early input cannot be lost.</summary>
public abstract class InteractivePageBase : ComponentBase
{
    protected bool IsInteractive { get; private set; }

    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender) return;
        IsInteractive = true;
        StateHasChanged();
    }
}
