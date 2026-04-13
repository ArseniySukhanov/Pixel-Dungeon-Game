using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class Map : MonoBehaviour
{
    private Transform _tileAnchor;
    /// <summary>
    /// Contains all sprites of the map
    /// </summary>
    public static Sprite[] Sprites { get; private set; }

    /// <summary>
    /// Colors of lights in rooms
    /// </summary>
    public static readonly Color[] Colors = { new Color(0.95f, 0.65f, 0.35f),new Color(0.03f, 0.6f, 0.58f) };
    /// <summary>
    /// Contains every Interior of the map
    /// </summary>
    private Interior[,] _interiors;
    /// <summary>
    /// Contains all room blocks on the map 
    /// </summary>
    private readonly List<Room> _leaves = new List<Room>();


    private Vector2 _offset;
    private static Vector2 _sizeMap;
       
    /// <summary>
    /// Contains whole structure of the modern map
    /// </summary>
    public static int[,] TileMap { get; private set; }


    
    /// <summary>
    /// Contains map (for debug purpose)
    /// </summary>
    [Header("Set in Inspector")]
    public TextAsset map; 
    /// <summary>
    /// Contains room presets
    /// </summary>
    public TextAsset[] roomTextAssets;
    /// <summary>
    /// Contains sprites for every tile in stacked together
    /// </summary>
    public Texture2D mapTiles;
    /// <summary>
    /// Preset of the floor tile
    /// </summary>
    public Tile tilePrefab;
    /// <summary>
    /// Preset of the wall tile
    /// </summary>
    public Interior interiorWallPrefab;

    public Enemy[] enemyPrefab;
    /// <summary>
    /// Value which defines how big room block could be
    /// </summary>
    /// <remarks> Size of the block is always bigger then maxRoomSize/3</remarks>
    [SerializeField] private int maxRoomSize = 50;
    /// <summary>
    /// Value which define chance of creating additional roads
    /// to minimal spanning trees of roads
    /// </summary>
    /// <remarks>Higher -> lower chance</remarks>
    [SerializeField] private int chanceToAddRoad = 10;

    private void Awake()
    {
        MapGenerate();
        // Debug options
        // WriteInFile("output.txt");
    }
    
    public enum Direction
    {
        Up,
        UpRight,
        Right,
        DownRight,
        Down,
        DownLeft,
        Left,
        UpLeft
    }

    /// <summary>
    /// Room block class
    /// </summary>
    public class Room
    {
        public Vector2 Point { get; }

        public Vector2 Size { get; }

        /// <summary>
        /// Filling of room block
        /// </summary>
        public int[,] Cells { get; }
            
        /// <summary>
        /// Initializes room block
        /// </summary>
        /// <param name="point">Block's coordinates</param>
        /// <param name="size">Block's width and height</param>
        public Room(Vector2 point, Vector2 size)
        {
            this.Point = point;
            this.Size = size;
            Cells = new int[(int) Size.x, (int) Size.y];
            RoomEmpty();
        }


        /// <summary>
        /// Fills it with empty tiles 
        /// </summary>
        private void RoomEmpty()
        {
            for(var i = 0; i < Size.x; i++)
            for (var j = 0; j < Size.y; j++)
            {
                Cells[i, j] = -1;
            }
        }

        /// <summary>
        /// Class of road which connect rooms
        /// </summary>
        public class Road
        {
            /// <summary>
            /// Room to which road is directed
            /// </summary>
            /// <remarks>In game roads are not directed, because
            /// they are always created with their pairs</remarks>
            public Room RoomTo { get; }
            /// <summary>
            /// Room from which road is directed
            /// </summary>
            /// <remarks>In game roads are not directed, because
            /// they are always created with their pairs</remarks>
            public Room RoomFrom { get; }
            /// <summary>
            /// Length of the road
            /// </summary>
            public readonly int Length;
            /// <summary>
            /// Side of the room which road is directed to
            /// </summary>
            public readonly Direction Side;
            /// <summary>
            /// Position of the road and the room border crossing
            /// </summary>
            public Vector2 RoadCentre;
            /// <summary>
            /// Shows if this road should be kept after removing
            /// excess roads
            /// </summary>
            public bool Chosen { get; private set; } = false;
            
            /// <summary>
            /// Makes road chosen to not be removed
            /// </summary>
            /// <param name="repeat">Shows if that same road directed
            /// backwards should be chosen</param>
            public void MakeChosen(bool repeat = true)
            {
                Chosen = true;
                if (!repeat) return;
                for(var i = 0; i < RoomTo.Roads.Count; i++)
                    if (RoomTo.Roads[i].RoomTo == RoomFrom)
                        RoomTo.Roads[i].MakeChosen(false) ;
            }
            /// <summary>
            /// Initializes road block
            /// </summary>
            /// <param name="roomTo">Room to which road is directed</param>
            /// <param name="roomFrom">Room from which road is directed</param>
            /// <param name="length">Length of the road</param>
            /// <param name="side">Side of the room in which road is directed to</param>
            /// <param name="roadCentre">Position of the road and room border crossing</param>
            public Road(Room roomTo, Room roomFrom, int length, Direction side, Vector2 roadCentre)
            {
                RoomTo = roomTo;
                RoomFrom = roomFrom;
                Length = length;
                Side = side;
                RoadCentre = roadCentre;
            }
        }
        
        /// <summary>
        /// All roads that go out of the room
        /// </summary>
        public List<Road> Roads { get; private set; }
        
        /// <summary>
        /// Add roads to both rooms which they are links
        /// </summary>
        /// <param name="room">Room which is connected by the road with calling method room</param>
        /// <param name="side">Side of the room in which road is directed to</param>
        /// <param name="roadCentre">Position of road and room border crossing</param>
        /// <param name="repeat">Show if we should inform the room which it leads to that it should also add the road to
        /// calling method room (mostly used in order to create undirected road)</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="side"/> has not appropriate value</exception>
        public void AddRoad(Room room, Direction side, Vector2 roadCentre, bool repeat = true)
        {
            Roads ??= new List<Road>();
            var tLength = Math.Abs(((int) Size.x - (int) room.Size.x) / 2 + (int) Point.x - (int) room.Point.x)+ Math.Abs(((int) Size.y - (int) room.Size.y) / 2 + (int) Point.y - (int) room.Point.y);
            Roads.Add(new Road(room, this, tLength, side, roadCentre));
            if (!repeat) return;
            switch (side)
            {
                case Direction.Up:
                    room.AddRoad(this, Direction.Down, new Vector2(roadCentre.x, roadCentre.y + 1), false);
                    break;
                case Direction.Down:
                    room.AddRoad(this, Direction.Up, new Vector2(roadCentre.x, roadCentre.y - 1), false);
                    break;
                case Direction.Right:
                    room.AddRoad(this, Direction.Left, new Vector2(roadCentre.x + 1, roadCentre.y), false);
                    break;
                case Direction.Left:
                    room.AddRoad(this, Direction.Right, new Vector2(roadCentre.x - 1, roadCentre.y), false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }
        
        /// <summary>
        /// Delete roads which are not chosen
        /// </summary>
        public void CleanRoads()
        {
            for (var i = 0; i < Roads.Count;)
            {
                if (Roads[i].Chosen)
                    i++;
                else
                    Roads.Remove(Roads[i]);
            }
        }
        
        /// <summary>
        /// Draws parts of the roads which are inside room block
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Some problems with some road side value</exception>
        public void DrawRoadsPart()
        {
            for (var i = 0; i < Roads.Count; i++)
            {
                var tRoad = Roads[i];
                int centerX=(int) Size.x/2, centerY = (int) Size.y/2;
                switch (tRoad.Side)
                {
                    case Direction.Up:
                        if (centerX < (int) tRoad.RoadCentre.x - (int)Point.x)
                        {
                            for (var j = centerX; j < tRoad.RoadCentre.x - (int) Point.x + 2; j++)
                            {
                                if (Cells[j, centerY] > 223 || Cells[j, centerY] < 0)
                                    Cells[j, centerY] = 0;
                                if (Cells[j, centerY - 1] > 223 || Cells[j, centerY - 1] < 0)
                                    Cells[j, centerY - 1] = 0;
                                if (Cells[j, centerY + 1] > 223 || Cells[j, centerY + 1] < 0)
                                    Cells[j, centerY + 1] = 240;
                            }
                           
                            for (var j = centerY; j < tRoad.RoadCentre.y - (int) Point.y + 1; j++)
                            {
                                if (Cells[(int) tRoad.RoadCentre.x - (int) Point.x, j] > 223 ||
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x, j] < 0)
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x, j] = 0;
                                if (Cells[(int) tRoad.RoadCentre.x - (int) Point.x + 1, j] > 223 ||
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x + 1, j] < 0)
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x + 1, j] = 0;
                                if (Cells[(int) tRoad.RoadCentre.x - (int) Point.x - 1, j] > 223 ||
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x - 1, j] < 0)
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x - 1, j] = 240;
                            }
                        }
                        else
                        {
                            for (var j = centerX; j > tRoad.RoadCentre.x - (int) Point.x - 1; j--)
                            {
                                if (Cells[j, centerY] > 223 || Cells[j, centerY] < 0)
                                    Cells[j, centerY] = 0;
                                if (Cells[j, centerY - 1] > 223 || Cells[j, centerY - 1] < 0)
                                    Cells[j, centerY - 1] = 0;
                                if (Cells[j, centerY + 1] > 223 || Cells[j, centerY + 1] < 0)
                                    Cells[j, centerY + 1] = 240;
                            }
                            for (var j = centerY - 1; j < tRoad.RoadCentre.y - (int) Point.y + 1; j++)
                            {
                                if (Cells[(int) tRoad.RoadCentre.x - (int) Point.x, j] > 223 ||
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x, j] < 0)
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x, j] = 0;
                                if (Cells[(int) tRoad.RoadCentre.x - (int) Point.x + 1, j] > 223 ||
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x + 1, j] < 0)
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x + 1, j] = 0;
                                if (Cells[(int) tRoad.RoadCentre.x - (int) Point.x - 1, j] > 223 ||
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x - 1, j] < 0)
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x - 1, j] = 240;
                            }
                        }
                        break;
                    case Direction.Down:
                        if (centerX < (int) tRoad.RoadCentre.x - (int)Point.x)
                        {
                            for (var j = centerY; j > tRoad.RoadCentre.y - (int) Point.y - 1; j--)
                            {
                                if (Cells[(int) tRoad.RoadCentre.x - (int) Point.x, j] > 223 ||
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x, j] < 0)
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x, j] = 0;
                                if (Cells[(int) tRoad.RoadCentre.x - (int) Point.x + 1, j] > 223 ||
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x + 1, j] < 0)
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x + 1, j] = 0;
                                if (Cells[(int) tRoad.RoadCentre.x - (int) Point.x - 1, j] > 223 ||
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x - 1, j] < 0)
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x - 1, j] = 240;
                            }
                            for (var j = centerX; j < tRoad.RoadCentre.x - (int) Point.x + 2; j++)
                            {
                                if (Cells[j, centerY] > 223 || Cells[j, centerY] < 0)
                                    Cells[j, centerY] = 0;
                                if (Cells[j, centerY - 1] > 223 || Cells[j, centerY - 1] < 0)
                                    Cells[j, centerY - 1] = 0;
                                if (Cells[j, centerY + 1] > 223 || Cells[j, centerY + 1] < 0)
                                    Cells[j, centerY + 1] = 240;
                            }
                            
                        }
                        else
                        {
                            for (var j = centerX; j > tRoad.RoadCentre.x - (int) Point.x - 1; j--)
                            {
                                if (Cells[j, centerY] > 223 || Cells[j, centerY] < 0)
                                    Cells[j, centerY] = 0;
                                if (Cells[j, centerY - 1] > 223 || Cells[j, centerY - 1] < 0)
                                    Cells[j, centerY - 1] = 0;
                                if (Cells[j, centerY + 1] > 223 || Cells[j, centerY + 1] < 0)
                                    Cells[j, centerY + 1] = 240;
                            }
                            for (var j = centerY; j > tRoad.RoadCentre.y - (int) Point.y - 1; j--)
                            {
                                if (Cells[(int) tRoad.RoadCentre.x - (int) Point.x, j] > 223 ||
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x, j] < 0)
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x, j] = 0;
                                if (Cells[(int) tRoad.RoadCentre.x - (int) Point.x + 1, j] > 223 ||
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x + 1, j] < 0)
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x + 1, j] = 0;
                                if (Cells[(int) tRoad.RoadCentre.x - (int) Point.x - 1, j] > 223 ||
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x - 1, j] < 0)
                                    Cells[(int) tRoad.RoadCentre.x - (int) Point.x - 1, j] = 240;
                            }
                        }

                        break;
                    case Direction.Right:
                        if (centerY < (int) tRoad.RoadCentre.y - (int)Point.y)
                        {
                            for (var j = centerX; j < tRoad.RoadCentre.x - (int) Point.x + 1; j++)
                            {
                                if (Cells[j ,(int) tRoad.RoadCentre.y - (int) Point.y] > 223 ||
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y] < 0)
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y] = 0;
                                if (Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y + 1] > 223 ||
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y + 1] < 0)
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y + 1] = 240;
                                if (Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y - 1] > 223 ||
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y - 1] < 0)
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y - 1] = 0;
                            }
                            for (var j = centerY; j < tRoad.RoadCentre.y - (int) Point.y + 1; j++)
                            {
                                if (Cells[centerX, j] > 223 || Cells[centerX, j] < 0)
                                    Cells[centerX, j] = 0;
                                if (Cells[centerX - 1, j] > 223 || Cells[centerX - 1, j] < 0)
                                    Cells[centerX - 1, j] = 240;
                                if (Cells[centerX + 1, j] > 223 || Cells[centerX + 1, j] < 0)
                                    Cells[centerX + 1, j] = 0;
                            }
                        }
                        else
                        {
                            for (var j = centerX; j < tRoad.RoadCentre.x - (int) Point.x + 1; j++)
                            {
                                if (Cells[j ,(int) tRoad.RoadCentre.y - (int) Point.y] > 223 ||
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y] < 0)
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y] = 0;
                                if (Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y + 1] > 223 ||
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y + 1] < 0)
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y + 1] = 240;
                                if (Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y - 1] > 223 ||
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y - 1] < 0)
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y - 1] = 0;
                            }
                            for (var j = centerY; j > tRoad.RoadCentre.y - (int) Point.y - 2; j--)
                            {
                                if (Cells[centerX, j] > 223 || Cells[centerX, j] < 0)
                                    Cells[centerX, j] = 0;
                                if (Cells[centerX - 1, j] > 223 || Cells[centerX - 1, j] < 0)
                                    Cells[centerX - 1, j] = 240;
                                if (Cells[centerX + 1, j] > 223 || Cells[centerX + 1, j] < 0)
                                    Cells[centerX + 1, j] = 0;
                            }
                        }
                        break;
                    case Direction.Left:
                        if (centerY < (int) tRoad.RoadCentre.y - (int)Point.y)
                        {
                            for (var j = centerX + 1; j > tRoad.RoadCentre.x - (int) Point.x - 1; j--)
                            {
                                if (Cells[j ,(int) tRoad.RoadCentre.y - (int) Point.y] > 223 ||
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y] < 0)
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y] = 0;
                                if (Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y + 1] > 223 ||
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y + 1] < 0)
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y + 1] = 240;
                                if (Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y - 1] > 223 ||
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y - 1] < 0)
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y - 1] = 0;
                            }
                            for (var j = centerY; j < tRoad.RoadCentre.y - (int) Point.y + 1; j++)
                            {
                                if (Cells[centerX, j] > 223 || Cells[centerX, j] < 0)
                                    Cells[centerX, j] = 0;
                                if (Cells[centerX - 1, j] > 223 || Cells[centerX - 1, j] < 0)
                                    Cells[centerX - 1, j] = 240;
                                if (Cells[centerX + 1, j] > 223 || Cells[centerX + 1, j] < 0)
                                    Cells[centerX + 1, j] = 0;
                            }
                        }
                        else
                        {
                            for (var j = centerX; j > tRoad.RoadCentre.x - (int) Point.x - 1; j--)
                            {
                                if (Cells[j ,(int) tRoad.RoadCentre.y - (int) Point.y] > 223 ||
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y] < 0)
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y] = 0;
                                if (Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y + 1] > 223 ||
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y + 1] < 0)
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y + 1] = 240;
                                if (Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y - 1] > 223 ||
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y - 1] < 0)
                                    Cells[j, (int) tRoad.RoadCentre.y - (int) Point.y - 1] = 0;
                            }
                            for (var j = centerY; j > tRoad.RoadCentre.y - (int) Point.y - 2; j--)
                            {
                                if (Cells[centerX, j] > 223 || Cells[centerX, j] < 0)
                                    Cells[centerX, j] = 0;
                                if (Cells[centerX - 1, j] > 223 || Cells[centerX - 1, j] < 0)
                                    Cells[centerX - 1, j] = 240;
                                if (Cells[centerX + 1, j] > 223 || Cells[centerX + 1, j] < 0)
                                    Cells[centerX + 1, j] = 0;
                            }
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        
        /// <summary>
        /// Generates enemies in the room
        /// </summary>
        /// <param name="enemyPrefab">Array with possible enemies</param>
        public void GenerateEnemies(ref Enemy[] enemyPrefab)
        {
            var numEnemies = Random.Range(1, 4);
            var centreX = (int)Size.x / 2 + (int)Point.x;
            var xs = new int[]
            {
                centreX + 2,
                centreX + 2,
                centreX - 2,
                centreX - 2,
            };
            var centreY = (int)Size.y / 2 + (int)Point.y;
            var ys = new int[]
            {
                centreY + 2,
                centreY - 2,
                centreY - 2,
                centreY + 2,
            };
            for (var i = 0; i < numEnemies; i++)
            {
                var enemy = Instantiate(enemyPrefab[Random.Range(0, enemyPrefab.Length)]);
                enemy.transform.position = 0.5f * Vector2.up + Tile.TileToTrans(new Vector2(xs[i], ys[i]));
            }
        }
    }




        
    /// <summary>
    /// Generates map
    /// </summary>
    private void MapGenerate()
    {
        _sizeMap = new Vector2(256, 256);
        GenerateTree();
        FillRooms();
        GenerateLinks();
        GenerateRoads();
        CleanRoads();
        AddEverythingToMap();
        ShowWholeMap();
    }

    /// <summary>
    /// Generates 2D BSPTree in order to create room zones
    /// </summary>
    /// <param name="size">Size of accessible surface used by BSPTree generation algorithm in order to understand how to derive rooms</param>
    /// <param name="point">Position of block on the map which should be processed by this instance of the function</param>
    private void GenerateTree( Vector2 size = default, Vector2 point = default)
    {
        if (size == Vector2.zero)
            size = _sizeMap;
        if ((size.x > maxRoomSize) && (size.x > size.y))
        {
            int tSize = (int) size.x / 3;
            tSize = tSize + Random.Range(0, tSize + 1);
            GenerateTree(new Vector2(tSize, (int) size.y), point);
            GenerateTree(new Vector2((int) size.x - tSize, (int) size.y),
                new Vector2((int) point.x + tSize, (int) point.y));
        }
        else
        {
            if (size.y > maxRoomSize)
            {
                int tSize = (int) size.y / 3;
                tSize = tSize + Random.Range(0, tSize + 1);
                GenerateTree(new Vector2((int) size.x, tSize), point);
                GenerateTree(new Vector2((int) size.x, (int) size.y - tSize),
                    new Vector2((int) point.x, (int) point.y + tSize));
            }
            else
            {
                _leaves.Add(new Room(point, size));
            }
        }
    }
        
    /// <summary>
    /// Adds rooms' patterns inside room blocks
    /// </summary>
    private void FillRooms()
    {
        foreach (var tRoom in _leaves)
        {
            var typeRoom = Random.Range(0,roomTextAssets.Length);
            var lines = roomTextAssets[typeRoom].text.Split(new [] { '\r', '\n' }, StringSplitOptions.None);
            const NumberStyles hex = System.Globalization.NumberStyles.HexNumber;
            for (var i = 0; i < lines.Length; i++)
            {
                var numTiles = lines[i].Split(' ');
                for (var j = 0; j < numTiles.Length; j++)
                {
                    if(numTiles[j] != "..")
                        tRoom.Cells[i+((int)tRoom.Size.x - lines.Length)/2, j +((int)tRoom.Size.y - numTiles.Length)/2] = int.Parse(numTiles[j], hex);
                }
            }
        }
    }

    /// <summary>
    /// Generates templates of all possible roads between room blocks
    /// </summary>
    private void GenerateLinks()
    {
        var size = _leaves.Count;
        for (var i = 0; i < size - 1; i++)
        for (var j = i + 1; j < size; j++)
        {
            if ((int) _leaves[i].Point.y == (int) _leaves[j].Point.y + (int) _leaves[j].Size.y)
            {
                var tRoadY =
                    (int) Math.Min(_leaves[i].Point.x + _leaves[i].Size.x, _leaves[j].Point.x + _leaves[j].Size.x) -
                    (int) Math.Max(_leaves[i].Point.x, _leaves[j].Point.x);
                if (tRoadY <= 4) continue;
                tRoadY =
                    ((int) Math.Min(_leaves[i].Point.x + _leaves[i].Size.x, _leaves[j].Point.x + _leaves[j].Size.x) +
                     (int) Math.Max(_leaves[i].Point.x, _leaves[j].Point.x)) / 2;
                _leaves[i].AddRoad(_leaves[j], Direction.Down, new Vector2(tRoadY, _leaves[i].Point.y));
            }
            else
            {
                if ((int) _leaves[j].Point.y == (int) _leaves[i].Point.y + (int) _leaves[i].Size.y)
                {
                    var tRoadY =
                        (int) Math.Min(_leaves[i].Point.x + _leaves[i].Size.x, _leaves[j].Point.x + _leaves[j].Size.x) -
                        (int) Math.Max(_leaves[i].Point.x, _leaves[j].Point.x);
                    if (tRoadY <= 4) continue;
                    tRoadY = ((int) Math.Min(_leaves[i].Point.x + _leaves[i].Size.x,
                                  _leaves[j].Point.x + _leaves[j].Size.x) +
                              (int) Math.Max(_leaves[i].Point.x, _leaves[j].Point.x)) / 2;
                    _leaves[j].AddRoad(_leaves[i], Direction.Down, new Vector2(tRoadY, _leaves[j].Point.y));
                }
                else
                {
                    if ((int) _leaves[i].Point.x == (int) _leaves[j].Point.x + (int) _leaves[j].Size.x)
                    {
                        var tRoadX =
                            (int) Math.Min(_leaves[i].Point.y + _leaves[i].Size.y,
                                _leaves[j].Point.y + _leaves[j].Size.y) -
                            (int) Math.Max(_leaves[i].Point.y, _leaves[j].Point.y);
                        if (tRoadX <= 4) continue;
                        tRoadX = ((int) Math.Min(_leaves[i].Point.y + _leaves[i].Size.y,
                                      _leaves[j].Point.y + _leaves[j].Size.y) +
                                  (int) Math.Max(_leaves[i].Point.y, _leaves[j].Point.y)) / 2;
                        _leaves[i].AddRoad(_leaves[j], Direction.Left,
                            new Vector2(_leaves[i].Point.x, tRoadX));
                    }
                    else
                    {
                        if ((int) _leaves[j].Point.x == (int) _leaves[i].Point.x + (int) _leaves[i].Size.x)
                        {
                            var tRoadX =
                                (int) Math.Min(_leaves[j].Point.y + _leaves[j].Size.y,
                                    _leaves[i].Point.y + _leaves[i].Size.y) -
                                (int) Math.Max(_leaves[j].Point.y, _leaves[i].Point.y);
                            if (tRoadX <= 4) continue;
                            tRoadX = ((int) Math.Min(_leaves[j].Point.y + _leaves[j].Size.y,
                                          _leaves[i].Point.y + _leaves[i].Size.y) +
                                      (int) Math.Max(_leaves[j].Point.y, _leaves[i].Point.y)) / 2;
                            _leaves[j].AddRoad(_leaves[i], Direction.Left,
                                new Vector2(_leaves[j].Point.x, tRoadX));
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    ///  Generates minimal spanning tree of roads using Kruskal algorithm
    /// </summary>
    private void GenerateRoads()
    {
        // Variable which includes all possible roads on the map, which are put in order
        var roads = new List<Room.Road>();
        // Includes rooms, roads of which are already considered
        var tLeaves = new List<Room>();
            
        // Fills roads in order
        for (var i = 0; i < _leaves.Count; i++)
        {
            var tRoom = _leaves[i];
            if (tRoom.Roads == null) continue;
            for (var k = 0; k < tRoom.Roads.Count; k++)
            {
                var tRoad = tRoom.Roads[k];
                if (tLeaves.Contains(tRoad.RoomTo)) continue;
                roads.Add(tRoad);
                if (roads.Count <= 1) continue;
                for (var j = roads.Count - 2; (j > -1) && tRoad.Length < roads[j].Length; j--)
                {
                    roads[j + 1] = roads[j];
                    roads[j] = tRoad;
                }
            }
            tLeaves.Add(tRoom);
        }

        var numLeaves = _leaves.Count;
        var numRoads = roads.Count;
        var tLink = new int[numLeaves];
        for (var i = 0; i < numLeaves; i++)
        {
            tLink[i] = i;
        }
        var numDiffLink = numLeaves - 1;
        for (var i = 0; /*i < numRoads &&*/ numDiffLink != 0; i++)
        {
            var t1 = _leaves.IndexOf(roads[i].RoomFrom);
            var t2 = _leaves.IndexOf(roads[i].RoomTo);
            if (tLink[t1] == tLink[t2]) continue;
            roads[i].MakeChosen();
            var t = tLink[t2];
            for (var j = 0; j < numLeaves; j++)
                if (tLink[j] == t)
                    tLink[j] = tLink[t1];
            numDiffLink--;
        }
    }

    /// <summary>
    /// Cleans nearly all non-chosen by GenerateRoads() roads (some are untouched in order to make more interesting layout)
    /// and draws them on room blocks
    /// </summary>
    private void CleanRoads()
    {
        for (var i = 0; i < _leaves.Count; i++)
        {
            if (Random.Range(1, chanceToAddRoad) == 1 && _leaves[i].Roads != null)
                _leaves[i].Roads[Random.Range(0, _leaves[i].Roads.Count)].MakeChosen();
        }

        for (var i = 0; i < _leaves.Count; i++)
        {
            _leaves[i].CleanRoads();
            _leaves[i].DrawRoadsPart();
        }
    }
        
    /// <summary>
    /// Copies map layout of every room block to the main map
    /// </summary>
    private void AddEverythingToMap()
    {
        TileMap = new int[256, 256];
        foreach (var tRoom in _leaves)
        {
            for (var i = 0; i < tRoom.Size.y; i++)
            for (var j = 0; j < tRoom.Size.x; j++)
            {
                TileMap[(int) tRoom.Point.x + j, (int) tRoom.Point.y + i] = tRoom.Cells[j, i];
            }
        }
    }
        
    /// <summary>
    /// Draws a map from all presented information
    /// </summary>
    private void ShowWholeMap()
    {
        var anchor = new GameObject("TILE_ANCHOR"); // Setting anchor to displace all tiles
        _tileAnchor = anchor.transform; // according to it

        Sprites = Resources.LoadAll<Sprite>(mapTiles.name);

        var randRoom = _leaves[Random.Range(0, _leaves.Count)];
        var anchorTileTr = randRoom.Point+randRoom.Size/2; // anchorTileTr - displacement of map start displacement in tiles coordinates
        Vector3 anchorTr = Tile.TileToTrans(anchorTileTr);
        _tileAnchor.position = new Vector3(0,(float)-0.5,0);
        transform.position = anchorTr+Vector3.back*10;
        GameObject.FindWithTag("Player").transform.position = anchorTr;
        _interiors = new Interior[256, 256];
        for (var i = 0; i < 256; i++)
        {
            for (var j = 0; j < 256; j++)
            {
                if (TileMap[i, j] == -1) continue;
                Interior ti;
                if (TileMap[i, j] < 224)
                {
                    ti = Instantiate(tilePrefab);
                    ti.SetSprite(TileMap[i, j]);
                }
                else
                {
                    ti = Instantiate(interiorWallPrefab);
                    ti.SetSprite(TileMap[i, j]);
                }

                ti.transform.SetParent(_tileAnchor);
                ti.SetInterior(i, j, TileMap[i, j]);
                _interiors[i, j] = ti;
            }
        }
        Enemy.OnMapCreation(); 
        for(var i = 0; i < _leaves.Count; i++)
            if(!_leaves[i].Equals(randRoom))
                _leaves[i].GenerateEnemies(ref enemyPrefab);
    }
        
//--Debug---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Debug function
    /// </summary>
    /// <param name="path">Path to file for check of room generation</param>
    /// <param name="size">Size of the map</param>
    private void WriteInFile(string path, Vector2 size = default)
    {
        if (size == Vector2.zero)
            size = _sizeMap;
        var writer = new StreamWriter(path);

        var tWholeMap = new char[(int) size.y][];

        for (var i = 0; i < size.y; i++)
        {
            tWholeMap[i] = new char[(int) size.x * 3];
            for (var j = 0; j < (int) size.x * 3; j += 3)
            {
                tWholeMap[i][j] = '.';
                tWholeMap[i][j + 1] = '.';
                tWholeMap[i][j + 2] = ' ';
            }
        }

        foreach (var tRoom in _leaves)
        {
            for (int j = 0; j < (int) tRoom.Size.y; j++)
            {
                tWholeMap[(int) tRoom.Point.y + j][(int) tRoom.Point.x * 3] = 'x';
                tWholeMap[(int) tRoom.Point.y + j][(int) tRoom.Point.x * 3 + 1] = 'x';
                tWholeMap[(int) tRoom.Point.y + j][(int) tRoom.Point.x * 3 + (int) tRoom.Size.x * 3 - 3] = 'x';
                tWholeMap[(int) tRoom.Point.y + j][(int) tRoom.Point.x * 3 + (int) tRoom.Size.x * 3 - 2] = 'x';
            }

            for (int j = 0; j < (int) tRoom.Size.x * 3; j += 3)
            {
                tWholeMap[(int) tRoom.Point.y][(int) tRoom.Point.x * 3 + j] = 'x';
                tWholeMap[(int) tRoom.Point.y][(int) tRoom.Point.x * 3 + j + 1] = 'x';
                tWholeMap[(int) tRoom.Point.y + (int) tRoom.Size.y - 1][(int) tRoom.Point.x * 3 + j] = 'x';
                tWholeMap[(int) tRoom.Point.y + (int) tRoom.Size.y - 1][(int) tRoom.Point.x * 3 + j + 1] = 'x';
            }
        }

        for (var i = 0; i < size.y; i++)
        {
            writer.WriteLine(new string(tWholeMap[i]));
        }

        writer.Close();
    }

    /// <summary>
    /// Generates tiles for a whole map using external generated file (using only for debug purposes)
    /// </summary>
    private void Show_Map_Debug()
    {
        var anchor = new GameObject("TILE_ANCHOR"); // Setting anchor to displace all tiles
        _tileAnchor = anchor.transform; // according to it

        Sprites = Resources.LoadAll<Sprite>(mapTiles.name);

        const NumberStyles hex = System.Globalization.NumberStyles.HexNumber;

        var lines = map.text.Split('\n');
        var tileNums = lines[256].Split(' ');
        Vector2 anchorTileTr; // anchorTileTr - displacement of map start displacement in tiles coordinates
        anchorTileTr.x = int.Parse(tileNums[0], hex);
        anchorTileTr.y = int.Parse(tileNums[1], hex);
        Vector3 anchorTr = Tile.TileToTrans(anchorTileTr);
        _tileAnchor.position = -anchorTr;
            
        TileMap = new int[256, 256];
        _interiors = new Interior[256, 256];
        for (int i = 0; i < 256; i++)
        {
            tileNums = lines[i].Split(' ');
            for (int j = 0; j < 256; j++)
            {
                if (tileNums[j] == "..")
                {
                    TileMap[i, j] = -1;
                }
                else
                {
                    TileMap[i, j] = int.Parse(tileNums[j], hex);
                    Interior ti;
                    if (TileMap[i, j] < 224)
                    {
                        ti = Instantiate(tilePrefab);
                        ti.SetSprite(TileMap[i, j]);
                    }
                    else
                    {
                        ti = Instantiate(interiorWallPrefab);
                        ti.SetSprite(TileMap[i, j]);
                    }

                    ti.transform.SetParent(_tileAnchor);
                    ti.SetInterior(i, j, TileMap[i, j]);
                    _interiors[i, j] = ti;
                }
            }
        }
    }
}