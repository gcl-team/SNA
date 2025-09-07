using SimNextgenApp.Core;
using SimNextgenApp.Demo.CustomModels;
using SimNextgenApp.Events;

namespace SimNextgenApp.Demo.RestaurantSample;

internal class FoodServingCompleteEvent(RestaurantModel model, Order order, Waiter waiter) : AbstractEvent
{
    public override void Execute(IRunContext context)
    {
        model.HandleFoodServingComplete(order, waiter, context);
    }
}
