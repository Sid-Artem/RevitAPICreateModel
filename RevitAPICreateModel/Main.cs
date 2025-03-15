using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Security.Cryptography;
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
                    AddRoof(doc, level1, walls);
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

        private void AddRoof(Document doc, Level level2, List<Wall> walls)
        {
            if (walls == null || walls.Count < 4)
                throw new Exception("Необходимо минимум 4 стены для создания крыши.");

           
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            if (roofType == null)
                throw new Exception("Не найден подходящий тип крыши.");

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;

            
            List<XYZ> points = new List<XYZ>
    {
        new XYZ(dt, -dt, 0),
        new XYZ(dt, dt, 0),
        new XYZ(-dt, dt, 0),
        new XYZ(-dt, -dt, 0),
        new XYZ(dt, -dt, 0)  
    };

            
            Application application = doc.Application;
            CurveArray curveArray = application.Create.NewCurveArray();

            for (int i = 0; i < 4; i++)
            {
                LocationCurve curve = walls[i].Location as LocationCurve;
                if (curve == null)
                    throw new Exception($"Стена {i} не содержит LocationCurve.");

                XYZ p1 = curve.Curve.GetEndPoint(0);
                XYZ p2 = curve.Curve.GetEndPoint(1);
                Line line = Line.CreateBound(p1 + points[i], p2 + points[i + 1]);
                curveArray.Append(line);
            }

            
            LocationCurve lastCurve = walls[3].Location as LocationCurve;
            LocationCurve firstCurve = walls[0].Location as LocationCurve;

            if (lastCurve == null || firstCurve == null)
                throw new Exception("Не удалось получить LocationCurve для последней или первой стены.");

            Line closingLine = Line.CreateBound(
                lastCurve.Curve.GetEndPoint(1) + points[3],
                firstCurve.Curve.GetEndPoint(0) + points[0]
            );
            curveArray.Append(closingLine);

        
            
                    ReferencePlane plane = doc.Create.NewReferencePlane(
                new XYZ(0, 0, 0),  
                new XYZ(0, 0, 1), 
                new XYZ(1, 0, 0),  
                doc.ActiveView
            );

            
            doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, -5000, 5000);
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
