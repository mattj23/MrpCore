# MrpCore

*MrpCore* is a set of C# core classes and algorithms for representing and operating on a foundational data model for MES (Manfuacturing Execution System) and MRP (Materials Resource Planning) systems.  This is not a MES or MRP system itself, but rather a building block for constructing one.

* It is written in C# 10 and targets NET 6.0.  
* It is built on the assumption that Entity Framework Core will be used for persistence.  
* It is only opinionated about core MES/MRP functions
* It uses constrained generic type parameters to allow you to extend the data model for a particular application

### Motivation

The motivation for *MrpCore* is the idea of a new generation of narrowly-scoped, fast to build, custom built MRP and MES systems that are tailored exactly to the needs of the user.  Currently, such systems are monolithic and built by vendors who are attempting to capture every client's needs in their operational model, resulting in something that "works" for everyone with enormous effort, but is tailored for no-one.  In my sixteen years in manufacturing it has occurred to me that the amount of effort which goes into adapting a MRP or MES to a user's needs is now as much as it would take to stand up a bespoke system from scratch, largely due to the continuous improvements in frameworks, tooling, and deployment technologies coming out of the software and DevOps worlds.  I've experimented with such bespoke systems before and they end up being much less frustrating for the end user and much more representative of the processes they intend to capture.  They also make such systems more accessible to smaller processes, allowing them to benefit from tools that were otherwise inaccessible.

*MrpCore* is an attempt to build a flexible core component that aids anyone experimenting with this concept of small, bespoke, targeted systems, by providing the minimal set of common functions while staying well out of the way of everything else.  It is antithetical to the philosophy of the current generation of systems, in which the user needs to bend their operational model to fit into the software.

## Core Concepts

### Manufacturing Execution System (MES)

A Manufacturing Execution System is effectively a replacement for the paper travelers that followed goods being produced through the production process.  It tracks what has been done and what needs to be done, and where each product is in the process as well as any relevant information or status associated with the product.


#### MES Core Concepts and Representation
The core concepts of a MES are:

1. The *product*, *item*, *good*, *workpiece*, etc. This is a single **unit** of finished product which is output from a manufacturing process.  Within *MrpCore* the base class which represents this concept is the `ProductUnitBase`.

2. The *part type* or *product type*.  A product always has a conceptual type.  This might be something with a Bill of Materials, engineering drawings, blueprints, perhaps an SKU.  It's not a physical item.  In software terms it's the type and not the instantiation.  Within *MrpCore* the base class which represents this concept is the `ProductTypeBase`. 

3. An explicit *state* or *status* attached to a physical product unit.  Examples of this might be "scrapped", or "trial".  This is in contrast to implicit states that can be deduced from other information, such as "in process", or "complete".  Explicit states are represented in *MrpCore* by the base class `UnitStateBase`.

4. The *route*, or *product route*.  A route is a sequence of operations which a physical product unit needs to successfully undergo in order to complete manufacturing. As with the distinction between "product type" and "product unit", there is a distinction between the theoretical route which any product of a certain type must undergo, and the actual route which the phyisical product unit undergos.  The former is a template, the latter is an actual sequence of requirements.  Optional steps or steps required only for a fractional sample of parts, for example, may be part of the route for the product type but will not appear on every actual product unit's route.  Units of the same type may also differ in their routes as some undergo rework, or get scrapped, etc.  Routes are represented by collections of two different base classes in *MrpCore*: `RouteOperationBase` for the conceptual route template for a product type, and `UnitOperationBase` for the route attached to an actual product unit.

5. The *result* of an operation.  This is what is recorded when an operation is performed and either passes or fails. It is recorded at a certain time, and references a specific operation which was on the physical product unit's route.  *MrpCore* represents this concept with the base class `OperationResultBase`.


#### MES Base Class Relations




