# Gibraltar.PostHog Library

This is a simple .NET Standard client for the PostHog analytics platform. It's designed for recording
events from your .NET application to PostHog so you can track user behavior and other events.  

## Installation

To activate the client, you need to provide at a minimum your API key, like this:

```csharp
var client = new PostHogApi("YOUR_API_KEY");
```

It's recommended you configure this as part of your dependency injection so it will get your logging and other configuration.

```csharp
// Add PostHog to your services
// If you're thinking this really should have its own options provider, etc. - you're right!
services.AddSingleton((provider) => new PostHogApi("YOUR_API_KEY"")); // Add your API key here
```

## Usage

The API provides three commands out of the box:

* **Capture**: Record an event in PostHog.  Everything ultimately routes into this.
* **Identify**: Publish information about the current user.  Ideally called before they are referenced on a Capture call.
* **Group**: Publish information about a group the current user is a member of.  Groups are typically tenants, customers, or companies within your application.

You can add more commands using extension methods to call the Capture method with a specific event name.