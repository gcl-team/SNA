# SNA

## Folder Structure

- [ ] SimNextgenApp.sln
  - [ ] SimNextgenApp/
    - [ ] Core/ \
       To contain the fundamental machinery and logic required to run any discrete event simulation, independent of the specific model being simulated.
    - [ ] Modeling/ \
      Base classes and interfaces for model building. \
      Intended to hold the core abstractions, base classes, and interfaces for specific simulation models.
    - [ ] Events/ \
      Specific event type definitions.
    - [ ] Distributions/ \
      Using MathNet.
    - [ ] Statistics/ \
      Utility for collecting stats, such as counts occurrences.
    - [ ] Configuration/ \
      Holds all parameters: distributions, counts, run length etc.
    - [ ] Exceptions/
      - [ ] SimulationException.cs
  - [ ] SimNextgenApp.Tests/
  - [ ] Examples/ \
    Separate Project for example models
