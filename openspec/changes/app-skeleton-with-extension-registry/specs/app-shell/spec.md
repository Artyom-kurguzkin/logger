## ADDED Requirements

### Requirement: Views are found by route through the registry
The app SHALL show the page of the registered view whose route matches the current route.

#### Scenario: Known route
- **WHEN** a view is registered with route `/logs` and the user opens `/logs`
- **THEN** the page of that view is shown

### Requirement: The side menu lists registered nav views in order
The side menu SHALL list every registered view marked for navigation, in the order they were registered.

#### Scenario: Two nav views
- **WHEN** views "Logs" and "Traces" are registered in that order, both marked for navigation
- **THEN** the side menu shows "Logs" first and "Traces" second

#### Scenario: View not for navigation
- **WHEN** a view is registered without the navigation mark
- **THEN** the side menu does not list it

### Requirement: A duplicate view id is refused
The registry MUST refuse to register a view whose id is already registered, and MUST keep the first one.

#### Scenario: Same id twice
- **WHEN** a second view is registered with an id that is already taken
- **THEN** the registration is refused with an error and the first view stays registered

### Requirement: An unknown route shows a no-such-view page
The app SHALL show a "no such view" page when no registered view matches the route.

#### Scenario: Unknown route
- **WHEN** the user opens a route that no view has
- **THEN** the "no such view" page is shown and the side menu stays usable
