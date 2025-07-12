using SimNextgenApp.Core;

namespace SimNextgenApp.Modeling.Generator;

internal interface IOperatableGenerator<TLoad>
{
    void HandleActivation(IRunContext context);
    void HandleDeactivation(IRunContext context);
    void HandleLoadGeneration(IRunContext context);
}
