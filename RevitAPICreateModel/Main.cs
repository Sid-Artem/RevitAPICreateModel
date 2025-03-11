using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAPICreateModel
{
    [Transaction(TransactionMode.Manual)]
    public class Main : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {

                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                Document doc = uidoc.Document;

                List<Level> listLevel = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .OfType<Level>()
                    .ToList();
                Level level1 = listLevel
                    .Where(x => x.Name.Equals("Уровень 1"))
                    .FirstOrDefault();

                Level level2 = listLevel
                    .Where(x => x.Name.Equals("Уровень 2"))
                    .FirstOrDefault();

                double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
                double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);
                double dx = width / 2;
                double dy = depth / 2;

                List<XYZ> points = new List<XYZ>();
                points.Add(new XYZ(-dx, -dy, 0));
                points.Add(new XYZ(dx, -dy, 0));
                points.Add(new XYZ(dx, dy, 0));
                points.Add(new XYZ(-dx, dy, 0));
                points.Add(new XYZ(-dx, -dy, 0));


                using (var ts = new Transaction(doc, "Create model"))
                {
                    ts.Start();


                    Getwalls(doc, points,level1,level2);


                    ts.Commit();

                }

            }

            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }


            return Result.Succeeded;


        }

        public List<Wall> Getwalls(Document doc, List<XYZ> points, Level level1, Level level2)

        {

            List<Wall> walls = new List<Wall>();


            for (int i = 0; i < points.Count - 1; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);


            }
            return walls;

        }








    }
}
