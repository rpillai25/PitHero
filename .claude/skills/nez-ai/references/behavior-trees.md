# Behavior Trees & Utility AI (Nez Built-ins)

PitHero doesn't currently use these, but Nez provides full implementations. Consult this reference if a future feature calls for them.

## Behavior Trees

### Composites
- **Sequence** — runs children in order, fails on first failure
- **Selector** — runs children in order, succeeds on first success
- **Parallel** — runs all children every tick, fails on first failure
- **RandomSequence** / **RandomSelector** — shuffled versions

### Decorators
- **AlwaysFail** / **AlwaysSucceed** — override child result
- **ConditionalDecorator** — gate child execution with a condition
- **Inverter** — flip child result
- **Repeater** — repeat N times
- **UntilFail** / **UntilSuccess** — loop until condition

### Actions
- **ExecuteAction** — wrap a `Func` as a leaf node
- **WaitAction** — delay for duration
- **LogAction** — debug logging
- **BehaviorTreeReference** — reference another tree

Use Nez's fluent builder API to assemble trees.

## Utility AI

Best for dynamic environments with many competing actions.

### Components
- **Reasoner** — root that selects the best consideration
- **Consideration** — contains appraisals + an action, produces a utility score
- **Appraisal** — calculates a sub-score for a consideration
- **Action** — what to execute when a consideration wins
