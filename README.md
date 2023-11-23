# BDS OnlineShop

BDS OnlineShop is an event-driven application based on Orleans actors and Orleans Streams.

## Table of Contents
- [Getting Started](#getting-started)
    * [Prerequisites](#prerequisites)
    * [New Orleans Users](#orleans)
    * [How to Run](#run)
- [Assignment](#assignment)
    * [Description](#description)
    * [Troubleshooting](#troubleshooting)

## <a name="getting-started"></a>Getting Started

### <a name="prerequisites"></a>Prerequisites

- IDE: [Visual Studio](https://visualstudio.microsoft.com/vs/community/) or [VSCode](https://code.visualstudio.com/)
- [.NET Framework 7](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)

### <a name="orleans"></a>New Orleans Users

[Orleans](https://learn.microsoft.com/en-us/dotnet/orleans/) framework provides facilities to program distributed applications at scale using the virtual actor model. We highly recommend starting from the [Orleans Documentation](https://learn.microsoft.com/en-us/dotnet/orleans/overview) to further understand the model.

### <a name="run"></a>How to Run

As any real-world application, we need to make sure the server is up and running before any client interation.
Therefore, in the project's root folder, run the following command:

```
dotnet run --project Server
```

This command will start up the Orleans server (aka silo).

Next, we can initialize the client program. In another console, run:

```
dotnet run --project Client
```

## <a name="exercise"></a>Assignment

### <a name="description"></a>Description

Refer to the description in Absalon.

### <a name="troubleshooting"></a>Troubleshooting

**Q: There are compilation errors**

**A:** Make sure you have installed .NET Framework 7 correctly. Besides, make sure you have not modified the original code.

**Q: How to debug?**

**A:** Use an IDEA. For instance, to open the project in Visual Studio, make sure to select the BDSOnlineShop.sln as the solution file, so Visual Studio will recognize the solution as a whole and allow you to debug your application.

**Q: The project is throwing exceptions.**

**A:** You are supposed to complete the application according to the assignment description, removing the exceptions thrown in the way.

**Q: My events are not reaching the correct actors.**

**A:** Make sure you are sending to the correct stream and actor. Refer to Orleans Streams documentation for further details.
