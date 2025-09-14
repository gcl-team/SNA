using SimNextgenApp.Core;
using SimNextgenApp.Demo.CustomModels;
using SimNextgenApp.Events;

namespace SimNextgenApp.Demo.RestaurantSample;

/// <summary>
/// This event fires at the simulation time when a servant has finished
/// walking a customer group to their table. It marks the end of the "seating"
/// activity.
/// </summary>
/// <param name="model">A reference to the main model to call its handler.</param>
/// <param name="group">The customer group that has been seated.</param>
/// <param name="table">The table where the group is now seated.</param>
/// <param name="waiter">The waiter who performed the seating action.</param>
internal class SeatingCompleteEvent(RestaurantModel model, CustomerGroup group, Table table, Waiter waiter) : AbstractEvent
{
    public override void Execute(IRunContext context)
    {
        // The event's only job is to call the appropriate handler on the model,
        // passing along all the context it has carried.
        model.HandleSeatingComplete(group, table, waiter, context);
    }
}
