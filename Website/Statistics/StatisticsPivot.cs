﻿
using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Internal.Web.Utils;

//  The implementation here is generic with respect to the specific properties in the data
//  if can produce any pivot from any data. The result of the pivot is a 2-d array because
//  that is easier to produce html tables from. The intermediary data structure is decorated
//  with sub-totals and sub-counts at every level. The sub-counts are there to simplify
//  the creation of html table rowspans.

namespace NuGetGallery.Statistics
{
    public class StatisticsPivot
    {
        public static void GroupBy(DownloadStatisticsReport report, string[] pivot)
        {
            // Firstly take the facts and the pivot vector and produce a tree structure

            Level level = InnerGroupBy(report.Facts, pivot);

            // Secondly print this tree structure into a sparse 2-dimensional structure (for creating html tables)
            // the structure is rectangular and not jagged. Logically this is a 2-d array however coding conventions
            // dictate the preference for jagged array structures. (Note generally this is only slightly sparse in our data.)

            for (int i = 0; i < level.Count; i++)
            {
                report.Table.Add(new TableEntry[pivot.Length + 1]);
            }

            PopulateTable(level, report.Table);
            report.Total = level.Total;
        }

        private static void PopulateTable(Level level, IList<TableEntry[]> table)
        {
            int row = 0;
            InnerPopulateTable(level, table, ref row, 0);
        }

        private static void InnerPopulateTable(Level level, IList<TableEntry[]> table, ref int row, int col)
        {
            foreach (KeyValuePair<string, Level> item in level.Next)
            {
                if (item.Value.Next == null)
                {
                    table[row][col] = new TableEntry { Data = item.Key };
                    table[row][col + 1] = new TableEntry { Data = item.Value.Amount.ToString(CultureInfo.InvariantCulture) };
                    row++;
                }
                else
                {
                    table[row][col] = new TableEntry { Data = item.Key, Rowspan = item.Value.Count };
                    InnerPopulateTable(item.Value, table, ref row, col + 1);
                }
            }
        }

        private static Level InnerGroupBy(IList<StatisticsFact> facts, string[] groupBy)
        {
            Level result = new Level(new Dictionary<string, Level>());

            foreach (StatisticsFact fact in facts)
            {
                Expand(result, fact, groupBy, 0);
                Assign(result, fact, groupBy, 0);
            }

            Count(result);
            Total(result);

            return result;
        }

        private static void Expand(Level result, StatisticsFact fact, string[] dimensions, int depth)
        {
            if (depth == dimensions.Length)
            {
                return;
            }

            if (!result.Next.ContainsKey(fact.Dimensions[dimensions[depth]]))
            {
                if (depth == dimensions.Length - 1)
                {
                    result.Next[fact.Dimensions[dimensions[depth]]] = new Level(0);
                    return;
                }
                else
                {
                    result.Next[fact.Dimensions[dimensions[depth]]] = new Level(new Dictionary<string, Level>());
                }
            }

            Expand(result.Next[fact.Dimensions[dimensions[depth]]], fact, dimensions, depth + 1);
        }

        private static void Assign(Level result, StatisticsFact fact, string[] dimensions, int depth)
        {
            if (depth == dimensions.Length)
            {
                return;
            }

            if (depth == dimensions.Length - 1)
            {
                int current = result.Next[fact.Dimensions[dimensions[depth]]].Amount;

                result.Next[fact.Dimensions[dimensions[depth]]].Amount = current + fact.Amount;
            }
            else
            {
                Assign(result.Next[fact.Dimensions[dimensions[depth]]], fact, dimensions, depth + 1);
            }
        }

        // The count in the tree is the count of values. It is equivallent to the count of rows if we
        // were to represent this in a table.

        private static int Count(Level level)
        {
            int count = 0;
            foreach (KeyValuePair<string, Level> item in level.Next)
            {
                if (item.Value.Next == null)
                {
                    count++;
                }
                else
                {
                    count += Count(item.Value);
                }
            }

            level.Count = count;

            return count;
        }

        // The total in a pivot can be found by adding up all the leaf nodes
        // The subtotals are also stored in the tree.

        private static int Total(Level level)
        {
            int total = 0;
            foreach (KeyValuePair<string, Level> item in level.Next)
            {
                if (item.Value.Next == null)
                {
                    //  Next is null this must therefore be a leaf node in the tree

                    total += item.Value.Amount;
                }
                else
                {
                    total += Total(item.Value);
                }
            }

            level.Total = total;

            return total;
        }

        public class TableEntry
        {
            public string Data { get; set; }
            public int Rowspan { get; set; }
            public string Uri { get; set; }

            public override bool Equals(object obj)
            {
                var other = obj as TableEntry;
                return other != null &&
                    String.Equals(Data, other.Data) &&
                    String.Equals(Uri, other.Uri) &&
                    Rowspan == other.Rowspan;
            }

            public override int GetHashCode()
            {
                return HashCodeCombiner.Start()
                    .Add(Data)
                    .Add(Rowspan)
                    .Add(Uri)
                    .CombinedHash;
            }
        }

        // This is for an internal data structure that represents the pivot as a tree.
        // An instance of a Level is a node in that tree. The Level can either contain
        // a dictionary of next Levels or an Amount. If Next is null then the Amount is valid
        // and the Level is a leaf node in the tree. The Count and Total fields are calculated 
        // and added to the tree after it has been constructed. They are correct with respect
        // to their subtree. The Count is useful for formatting RowSpan in HTML tables.

        private class Level
        {
            public Level(int amount)
            {
                Amount = amount;
            }

            public Level(IDictionary<string, Level> next)
            {
                Next = next;
            }

            // Either Next is not null or this is a leaf in the pivot tree, in which case Amount is valid

            public IDictionary<string, Level> Next { get; set; }
            public int Amount { get; set; }

            // Count is the count of child nodes in the tree - recursively so grandchildren etc. also get counted

            public int Count { get; set; }

            // Total is the sum Total of all the Amounts in all the decendents. (See Total function above.)

            public int Total { get; set; }
        }
    }
}
