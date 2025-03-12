using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
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


                    var walls= Getwalls(doc, points,level1,level2);
                    if (walls == null||walls.Count==0) 
                    {
                        throw new InvalidOperationException("Список стен пуст");
                    
                    }
                    AddDoors(doc, level1, walls[0]);

                    AddWindows(doc, level1, walls[1]);
                    AddWindows(doc, level1, walls[2]);
                    AddWindows(doc, level1, walls[3]);

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

        private void AddWindows(Document doc, Level level1, Wall wall)
        {
            if (wall == null)
            {
                throw new ArgumentNullException(nameof(wall), "Стена не может быть null");

            }
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0406 x 0610 мм"))
                .Where(x => x.FamilyName.Equals("Фиксированные"))
                .FirstOrDefault();


            if (windowType == null)
            {
                throw new InvalidOperationException("Не найден оконный тип");

            }

            if (!(wall.Location is LocationCurve hostCurve) || hostCurve.Curve == null)
            {
                throw new InvalidOperationException("Стена не имеет геометрии");
            }
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            double offset = UnitUtils.ConvertToInternalUnits(1.5, UnitTypeId.Meters);

            point=new XYZ(point.X, point.Y, point.Z+offset);


            if (!windowType.IsActive)
                windowType.Activate();
            doc.Create.NewFamilyInstance(point, windowType, wall, level1, StructuralType.NonStructural);
        }

        public void AddDoors(Document doc, Level level1, Wall wall)
        {

            if (wall == null)
            {
                throw new ArgumentNullException(nameof(wall), "Стена не может быть null");

            }
            FamilySymbol doorType= new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x=>x.Name.Equals("0915 x 2134 мм"))
                .Where(x=>x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();

            
         
          
            if (doorType == null)
            {
                throw new InvalidOperationException("Не найден дверной тип");

            }

            if(!(wall.Location is LocationCurve hostCurve)|| hostCurve.Curve == null)
            {
                throw new InvalidOperationException("Стена не имеет геометрии");
            }
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;



            if (!doorType.IsActive)
                doorType.Activate();
            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);


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
