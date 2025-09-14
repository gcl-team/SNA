using SimNextgenApp.Core;
using SimNextgenApp.Demo.CustomModels;
using SimNextgenApp.Events;

namespace SimNextgenApp.Demo.RestaurantSample;

internal class EatingCompleteEvent(RestaurantModel model, Table table) : AbstractEvent
{
    public override void Execute(IRunContext context)
    {
        model.HandleEatingComplete(table, context);
    }
}