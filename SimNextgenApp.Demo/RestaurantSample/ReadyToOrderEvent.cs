using SimNextgenApp.Core;
using SimNextgenApp.Demo.CustomModels;
using SimNextgenApp.Events;

namespace SimNextgenApp.Demo.RestaurantSample;

internal class ReadyToOrderEvent(RestaurantModel model, CustomerGroup group, Table table) : AbstractEvent
{
    public override void Execute(IRunContext context)
    {
        model.HandleReadyToOrder(group, table, context);
    }
}
