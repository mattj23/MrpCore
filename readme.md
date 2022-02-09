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

4. The *route*, or *product route*.  A route is a sequence of operations which a physical product unit needs to successfully undergo in order to complete manufacturing. As with the distinction between "product type" and "product unit", there is a distinction between the theoretical route which any product of a certain type must undergo, and the actual route which the physical product unit undergoes.  The former is a template, the latter is an actual sequence of requirements.  Optional steps or steps required only for a fractional sample of parts, for example, may be part of the route for the product type but will not appear on every actual product unit's route.  Units of the same type may also differ in their routes as some undergo rework, or get scrapped, etc.  Routes are represented by collections of two different base classes in *MrpCore*: `RouteOperationBase` for the conceptual route template for a product type (sometimes called the "master route" in industry), and `UnitOperationBase` for the route attached to an actual product unit.

5. The *result* of an operation.  This is what is recorded when an operation is performed and either passes or fails. It is recorded at a certain time, and references a specific operation which was on the physical product unit's route.  *MrpCore* represents this concept with the base class `OperationResultBase`.


#### MES Base Class Relations

The MES base classes have particular relations between each other, such that defining the database context for EFCore requires type arguments to specify them all at once.  The types themselves are allowed to be any custom class which inherits from the appropriate base class.

* A *Product Type* is a class which inherits from `ProductTypeBase`, which defines an integer id, name, and description for the type of product being produced.  This type references nothing directly.

* A *Product Unit* is a class which inherits from `ProductUnitBase<TProductType>`, which defines an integer id for a physical unit being created, as well as referencing the associated *Product Type* through a foreign key constraint. It also has a UTC timestamp of creation and a boolean flag specifying if the unit is archived (to reduce the burden on queries when a unit in a terminal state is moved out of inventory).

* A *Route Operation* is a class which inherits from `RouteOperationBase<TProductType>` which conceptually defines an operation on the "master" route for all units of a particular product type. It references a single *Product Type* through a foreign key constraint, while also having many fields specifying how it gets added to a route, what happens on a failed attempt, and a sequence number to determine order, as well as any explicit states which get added or removed by the operation.

* A *Unit Operation* is a class which inherits from `UnitOperationBase<TProductType, TProductUnit, TRouteOperation>`, and it links a specific *Product Unit* to a *Route Operation*.  The collection of *Unit Operations* is effectively the individual unit's route, as it determines which operations need to be successfully performed on a specific *Product Unit* for it to be considered completed.  There is at most one *Unit Operation* per combination of *Product Unit* and *Route Operation* who share the same *Product Type*.

* An *Operation Result* is a class which inherits from `OperationResultBase<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation>`, and it represents the most complex relation.  The type argument list for `OperationResultBase` is the set of compatible types which together make up the MES system.  The *Operation Result* represents the result of performing a *Unit Operation*, and it has a timestamp and a boolean to represent whether or not the result was successful (did the operation pass?). There may be zero to many *Operation Results* attached to a single *Unit Operation*, as some operations may be permissible to retry.

* Lastly, a *Unit State* is a class which inherits from `UnitStateBase`.  This has no explicit relations with other classes, and in many cases may be suitable to use as-is without extension.  A *Unit State* represents an explicit state attached to a *Product Unit* at a specific stage in its process. *Unit States* can be thought of as tags attached and removed by completed *Unit Operations*.  They possess a name, a description, and two boolean flags: one which determines whether the presence of the state blocks the unit from being considered complete even if all of the operations are finished successfully, and one which determines whether the presence of the state terminates the unit's route (such as scrapping the unit).

#### EF Core and Type Arguments

The use of the type arguments in the template classes as shown above means that custom classes can be used to extend the base class features while still allowing a type-consistent set of tools that can handle extended classes and take and return the proper types.

However, with Entity Framework Core, this produces a challenge for navigation properties, as a pair of types with a relation would need to have navigation properties on both types, meaning that both types have to know about each other, resulting in a circular template argument reference.

To get around this, some navigation properties have been excluded, and the `MesManager<...>` class has been written to retrieve these relations manually.

#### Typical Base Class Extensions

The MES base classes represent the minimal features necessary to perform MES operations according to *MrpCore*'s operational model.  Anything which goes beyond this should be extended by inheriting from the base classes.  Some typical extension features might be:

* Adding a serial number to *Product Unit*
* Adding notes, references, documentation to *Product Type*
* Adding work instructions to *Route Operation*
* Adding notes, operator name or ID number to *Operation Result*

For example, extending *Product Type* and *Product Unit* might look as follows:

```c#
public class WidgetType : ProductTypeBase 
{
    public string CustomerName { get; set; }
}

public class WidgetUnit : ProductUnitBase<WidgetType> 
{
    public string SerialNumber { get; set; }
}
```

#### Routes and Ordering

The "master route" is represented by the collection of *Route Operations* attached to a specific *Product Type*. There are different types of *Route Operations* which have different behaviors:

* Standard route operations: these are standard operations whose sequence is determined by their op number. They may be required/default for all parts, or optionally added to some parts by the system or the user.  They represent an operation which must be completed, and can allow for different behavior on failure.

* Special route operations: these are special operations which are not part of a standard route, but rather can be added intentionally by a client (human or software) simply to add or remove an attached *Unit State*.  An example of this would be scrapping a unit, which is performed via a special route operation.

* Corrective route operations: a standard route operation can be configured to use one of three different options when a non-passing *Operation Result* occurs.
    1. Permit retries - the operation can be repeated up to some configurable number of retries
    2. Trigger special operation - a special operation which terminates the route can be invoked
    3. Corrective chain - a chain of corrective operations can be added to the route, upon successful completion of which the unit resumes the rest of the route

When a new *Product Unit* is created, the standard route operations which are set to be required/default are linked with *Unit Operations*. 

*Route Operations* which are not default can still be added to the *Product Unit*'s route by creating an appropriate *Unit Operation* after the fact. For instance, an inspection step which needs to be performed every fifth part can be set as an optional step, and then the client code can add it on appropriate units when they're created.

At any given time, the set of operations which a particular *Product Unit* needs to complete successfully are only the ones which have been added via the creation of an associating *Unit Operation*.  The order of these operations is determined by the op number of the referencing *Route Operation* for each *Unit Operation*.

#### Route Changes









