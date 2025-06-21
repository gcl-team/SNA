using System;
using System.Collections.Generic;
using System.Text;

namespace SimNextgenApp.Statistics;

/// <summary>
/// Defines a sink for receiving structured trace records from the SimulationEngine.
/// </summary>
public interface ISimulationTracer
{
    /// <summary>
    /// Receives a trace record from the simulation engine.
    /// </summary>
    void Trace(TraceRecord record);
}
