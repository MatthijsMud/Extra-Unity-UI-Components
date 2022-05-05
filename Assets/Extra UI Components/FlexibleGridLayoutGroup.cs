using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.UI // Namespace might need to be changed?
{
  /// <summary>Renders a flexible grid based on the</summary>
  /// <remarks>
  /// The width of each column is determined based on the widest cell in it.
  /// Rows determine their height in a similar fashion.
  /// <see cref=""/>
  /// </remarks>
  [AddComponentMenu("Layout/Flexible Grid Layout Group")]
  public sealed class FlexibleGridLayoutGroup : LayoutGroup
  {
    /// <summary>Lower bound for the number of columns that is configurable.</summary>
    /// <remarks>
    /// In order to place its cells, a grid needs at least one row and column;
    /// any less leaves it without options to place the items.
    /// </remarks>
    private const int MinNumberOfColumns = 1;

    /// <summary>Horizontal and vertical spacing between cells.</summary>
    [SerializeField]
    private Vector2 spacing;

    /// <summary>Backing field for <see cref="Columns"/></summary>
    /// <remarks>Do not use this field directly.</remarks>
    [SerializeField]
    [Min(MinNumberOfColumns)]
    private int _columns = MinNumberOfColumns;
    public int Columns {
      get => _columns;
      set => SetProperty(ref _columns, Math.Min(value, 1));
    }

    /// <summary>Calculated minimum, preferred and flexible width for each column.</summary>
    /// <remarks>
    /// Gets updated by <see cref="CalculateLayoutInputHorizontal" />.
    /// </remarks>
    private IReadOnlyList<(float min, float preferred, float flexible)> columnWidths;
    /// <summary>Calculated minimum, preferred and flexible height for each row.</summary>
    /// <remarks>
    /// Gets updated by <see cref="CalculateLayoutInputVertical" />.
    /// </remarks>
    private IReadOnlyList<(float min, float preferred, float flexible)> rowHeights;

    private IEnumerable<(RectTransform component, (int column, int row) index)> Cells {
      get => rectChildren.Select((child, index) => (child, (index % Columns, index / Columns)));
    }


    public override void CalculateLayoutInputHorizontal()
    {
      // Required to initialize `rectChildren`.
      base.CalculateLayoutInputHorizontal();

      columnWidths = CalculateLayoutAlongAxis(0, padding.left + padding.right, spacing.x);
    }

    public override void CalculateLayoutInputVertical()
    {
      rowHeights = CalculateLayoutAlongAxis(1, padding.top + padding.bottom, spacing.y);
    }

    private IReadOnlyList<(float min, float preferred, float flexible)> CalculateLayoutAlongAxis(
      int axis, 
      float padding,
      float spacing
    ) 
    {
      var children = rectChildren;
      var count = children.Count();

      // No point in calculating the size of children if there are none.
      if (count == 0) { return new (float,float,float)[0]; }

      var columns = Columns;
      // Round up to acocunt for a partially filled last row.
      var rows = ((count - 1) / columns) + 1;

      var cellSizes = new (float min, float preferred, float flexible)[axis == 0 ? columns : rows];

      foreach (var (child, (column, row)) in Cells)
      {
        var sizeIndex = axis == 0 ? column : row;
        var current = cellSizes[sizeIndex];

        var required = Mathf.Max(
          current.min, 
          LayoutUtility.GetMinSize(child, axis)
        );
        var preferred = Mathf.Max(
          current.preferred,
          // Assume a cell wants to be at least as big as its minimum size,
          // even if no preference is specified. This to prevent the column/row 
          // from "stealing" space allocated for a different column/row that
          // did specify a preference for its size.
          required, 
          LayoutUtility.GetPreferredSize(child, axis)
        );
        var flexible = Mathf.Max(
          current.flexible, 
          LayoutUtility.GetFlexibleSize(child, axis)
        );

        // Determine the size of the widest/heighest component in each row/column,
        // as that size will be used for the column in question.
        cellSizes[sizeIndex] = (required, preferred, flexible);

      }
      var (totalRequired, totalPreferred, totalFlexible) = CalculateTotalLineSize(
        padding,
        spacing,
        cellSizes
      );

      SetLayoutInputForAxis(totalRequired, totalPreferred, totalFlexible, axis);
      return cellSizes;
    }
    
    private static (float min, float preferred, float flexible) CalculateTotalLineSize(
      float padding, 
      float spacing, 
      IReadOnlyList<(float, float, float)> cellSizes
    )
    {
      var (min, preferred, flexible) = cellSizes.Aggregate((0f, 0f, 0f), (acc, current) =>
      {
        var (totalMin, totalPreferred, totalFlexible) = acc;
        var (min, preferred, flexible) = current;
        return (
          totalMin + min, 
          totalPreferred + preferred,
          totalFlexible + flexible
        );
      });
      // Prevent the grid from shrinking smaller than its padding in the
      // unlikely scenario it does not contain any cells.
      var totalSpacing = Mathf.Max(0, cellSizes.Count() - 1) * spacing;
      return (
        min + totalSpacing + padding, 
        preferred + totalSpacing + padding, 
        // Determines how much of the remaining space the component is willing to take.
        // Padding and spacing has already been accounted for in the minimum/preferred size.
        flexible
      );
    }

    public override void SetLayoutHorizontal()
    {
      foreach (var (cell, (position, size)) in CalculateCells(
        available: rectTransform.rect.width,
        padding: (padding.left, padding.right),
        spacing: spacing.x,
        cells: Cells.Select(cell => (cell.component, cell.index.column)),
        sizes: columnWidths
      ))
      {
        SetChildAlongAxis(cell, 0, position, size);
      }
    }

    public override void SetLayoutVertical()
    {
      foreach (var (cell, (position, size)) in CalculateCells(
        available: rectTransform.rect.height,
        padding: (padding.top, padding.bottom),
        spacing: spacing.y,
        cells: Cells.Select(cell => (cell.component, cell.index.row)),
        sizes: rowHeights
      ))
      {
        SetChildAlongAxis(cell, 1, position, size);
      }
    }

    private static IEnumerable<(RectTransform, (float position, float size))> CalculateCells(
      float available,
      (float start, float end) padding,
      float spacing,
      // The number is used to indicate which cell in the row/column gets 
      // occupied by the component.
      IEnumerable<(RectTransform, int)> cells,
      IReadOnlyList<(float min, float preferred, float flexible)> sizes
    )
    {
      var (
        totalRequired, 
        totalPreferred, 
        totalFlexible
      ) = CalculateTotalLineSize(padding.start + padding.end, spacing, sizes);

      var remaining = available - totalRequired;
      var ideally = totalPreferred - totalRequired;
      // After reaching their preferred size, rows/columns might want to grow
      // at a different rate. Account for this by taking at most the size
      // required to accomodate each row's/column's preferred size; this should
      // however not exceed the remaining amount of space, as that would lead to
      // cells overlapping one another.
      var reservedForPreference = Math.Min(remaining, ideally);
      remaining = Mathf.Max(remaining - reservedForPreference, 0);

      // If none of the cells in this axis specify how much flexible space they 
      // are willing to take (or specify it as `0f`), some 0 divisions might 
      // occur; thus introducing `NaN`. Prevent this from occurring.
      totalFlexible = totalFlexible == 0f ? totalFlexible : 1;

      var totalOffset = padding.start;
      var allocated = sizes.Select(size =>
      {
        var (requires, prefers, flexible) = size;
        var missing = prefers - requires;
        // Split remaining space for preference proportionally to how much
        // the cell contributed to the total. This as opposed to, for example,
        // using first-come-first-serve basis.
        var preferredShare = (missing / ideally) * reservedForPreference;
        var remainingShare = (remaining / totalFlexible) * flexible;

        var allocatedSize = requires + preferredShare + remainingShare;
        
        var offset = totalOffset;
        totalOffset += allocatedSize + spacing;
        return (offset, allocatedSize);
      }).
      ToArray();

      return cells.Select(cell =>
      {
        var (child, index) = cell;
        // It assumed that each index fits in the range 
        return (child, allocated[index]);
      });
    }

  }
}

// MIT License
//
// Copyright 2022 Matthijs Mud
//
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal 
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS 
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN 
// THE SOFTWARE.