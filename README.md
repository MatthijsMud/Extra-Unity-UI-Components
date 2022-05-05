# Extra [Unity UI][ugui] components

The Unity UI package provides a decent range of components that can be used for creating a graphical user interface. Quite complicated interfaces can easily be created between various visual and interactive components, as well as components used to create dynamic layouts.

Some designs might however benefit from a component or two that is provided by default. This repository contains a selection of components that might help with make those designs a reality.

## `FlexibleGridLayoutGroup`

Unity UI does provide a component for laying out UI elements in a grid - `GridLayoutGroup` - but that component is a tad limited in its uses. This due to it ignoring practically all properties of any `ILayoutComponent` its "cells" might have.

The `GridLayoutGroup` assigns each cell the same width and height. This could end up smaller than its `minWidth`/`minHeight`. Also, the space available to the grid is hardly used, despite some cells potentially benefitting from it (such long lines of text).

`FlexibleGridLayoutGroup` is intended as an improvement that does take the **minimum**, **preferred**, and **flexible** size of its cells into account. 

The **minimum**, **preferred**, and **flexible** size of each **column** and **row** is based on the the highest respective property; `width` based for **columns**, `height` for **rows**. Any amount of space available to the grid is 

1. Allocate each **row** and **column** its minimum `width` and `height`, based on respectively the widest element in the **column** and heighest element in the **row**
1. A portion of the remaining space (if any) is allocated to each **column** and **row** based on how much their **preferred** size contributed to the grid **preferred** size (compared to **minimum** size), up until they reach their preferred size.
1. Any **columns** and **rows** that have a **flexible** size other than `0` share the remaining amount of space in a ratio based on their **flexible** size.


### Options

| Property  | Default  | Description | 
|-----------|----------|-------------|
| `spacing` | `0`, `0` | Amount of pixels between each column (`x`) and row (`y`). |
| `columns` | `1` | Number of columns in the grid. Rows are automatically generated based on this value and the number of cells. |


### Known issues

- No known issues

[ugui]: https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/index.html