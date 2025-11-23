using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace AutoOverlay.Forms;

public class TransposeGridController<T>
{
    private readonly DataGridView grid;
    private readonly PropertyInfo[] properties;
    private IList<T> datasource;

    public TransposeGridController(DataGridView grid)
    {
        this.grid = grid;
        properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => !typeof(IEnumerable).IsAssignableFrom(p.PropertyType))
            .ToArray();
        datasource = new List<T>();

        //grid.CellFormatting += OnGridOnCellFormatting;

        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.IsCurrentCellDirty)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        grid.CellValueChanged += OnGridOnCellValueChanged;
    }

    public IList<T> DataSource
    {
        get => datasource;
        set
        {
            datasource = value;
            grid.Columns.Clear();
            grid.Rows.Clear();

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "prop",
                HeaderText = "Property",
                ReadOnly = true,
                Width = 150,
                DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.LightGray }
            });

            for (int i = 0; i < datasource.Count; i++)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = i.ToString(),
                    HeaderText = $"#{i + 1}",
                    Width = 120
                };
                grid.Columns.Add(col);
            }

            foreach (var prop in properties)
            {
                int rowIndex = grid.Rows.Add();
                var row = grid.Rows[rowIndex];
                row.Cells[0].Value = prop.Name;

                for (int i = 0; i < datasource.Count; i++)
                {
                    row.Cells[i + 1].Value = prop.GetValue(datasource[i]) ?? "";
                }
            }
            grid.Refresh();
        }
    }

    private void OnGridOnCellFormatting(object s, DataGridViewCellFormattingEventArgs e)
    {
        if (e.ColumnIndex <= 0 || e.RowIndex < 0) return;

        var prop = properties[e.RowIndex];

        var currentCell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];

        if (prop.PropertyType == typeof(bool) || prop.PropertyType == typeof(bool?))
        {
            if (currentCell is DataGridViewTextBoxCell)
            {
                var threeState = prop.PropertyType == typeof(bool?);
                var checkCell = new DataGridViewCheckBoxCell
                {
                    Value = threeState && (currentCell.Value == DBNull.Value || currentCell.Value == null)
                        ? null
                        : (bool?)currentCell.Value ?? false,
                    ThreeState = threeState
                };

                grid.Rows[e.RowIndex].Cells[e.ColumnIndex] = checkCell;

                e.Value = checkCell.Value;
                e.FormattingApplied = true;
            }
        }
    }

    private void OnGridOnCellValueChanged(object s, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex <= 0) return;

        int objIndex = e.ColumnIndex - 1;
        string propName = grid.Rows[e.RowIndex].Cells[0].Value.ToString();
        var prop = properties.First(p => p.Name == propName);
        var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        var cellValue = cell.Value;

        object newValue = cellValue;
        if (cellValue == null || cellValue == DBNull.Value)
            newValue = null;
        else if (prop.PropertyType != typeof(string) && cellValue is string str && string.IsNullOrWhiteSpace(str))
            newValue = null;
        else
        {
            try
            {
                newValue = Convert.ChangeType(cellValue, prop.PropertyType);
            }
            catch (FormatException ex)
            {
                cell.Style.BackColor = Color.LightPink;
                return;
            }
        }

        cell.Style.BackColor = Color.White;
        prop.SetValue(datasource[objIndex], newValue);
    }
}