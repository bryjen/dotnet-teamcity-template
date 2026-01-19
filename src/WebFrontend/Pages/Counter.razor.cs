using Microsoft.AspNetCore.Components;

namespace WebFrontend.Pages;

public partial class Counter : ComponentBase
{
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
