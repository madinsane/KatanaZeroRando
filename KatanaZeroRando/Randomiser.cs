using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using UndertaleModLib;
using UndertaleModLib.Models;

namespace KatanaZeroRando
{
    class Randomiser
    {
        //Prefix for all objects
        public const string OBJPREFIX = "obj_";
        //Prefix for all hardmode enemies
        public const string HARDPREFIX = "ghost_";
        //Settings path
        public const string SETTINGSPATH = "settings.ini";

        //Names of all valid enemy objects to add to the random pool (obj_ prefix is unnecessary)
        public static string[] enemyNames = { 
            "enemy", "enemy_grunt", "enemy_shotgun", "enemy_shieldcop", "enemy_meele", "enemy_redpomp",
            "enemy_cop", "enemy_sci", "enemy_machinegun"
        };
        //Names of all valid throwables
        public static string[] throwNames =
        {
            "obj_flamethrower", "obj_throwable_bust", "obj_throwable_lamp", "obj_throwable_butcher", "obj_throwable_plant",
            "obj_throwable_dec_sword", "obj_throwable_knife", "obj_throwable_pistol", "obj_throwable_beerbottle_1", "obj_throwable_beerbottle_2",
            "obj_throwable_beerbottle_3", "obj_throwable_beerbottle_4", "obj_throwable_explosive_vial", "obj_throwable_smoke_vial", 
            "obj_throwable_remote_mine"
        };
        //Names of rooms to not randomise
        public static string[] ignoredRooms =
        {
            "room_dragon_1_setup"
        };
        //Names of rooms with stairs (doors that warp), if a scientist uses one of these doors the game crashes so scientists are replaced by cops for those rooms
        public static string[] stairRooms =
        {
            "room_club_2", "room_haunted_silent1", "room_mansion_3", "room_chinatown_2", "room_chinatown_4", "room_chinatown_5", "room_chinatown_6",
            "room_chinatown_9", "room_dragon_3", "room_dragon_4", "room_factory2_2", "room_factory2_3", "room_factory2_4_2", "room_factory2_5",
            "room_bunker_7", "room_bunker_10", "room_bunker2_3"
        };
        //Stores the enemies to avoid being randomised
        public static List<Pair<string, ObjDesc>> blockedEnemies;
        //Stores the throwables to avoid being randomised
        public static List<Pair<string, ObjDesc>> blockedThrow;
        //Stores the mapping of weights to enemy type for random generation
        public static Dictionary<string, int> weights;
        //Stores the total weight of all enemies
        public static int totalWeight;
        //Path to data.win input
        public static string dataWinPath;
        //Path to data.win output
        public static string outputPath;
        //Randomise enemies setting
        public static bool randomEnemies;
        //Randomise throwables setting
        public static bool randomThrowables;
        //Stores the read result of the data.win input
        public static UndertaleData data;
        //Stores the object definitions for each enemy type in enemyNames
        public static Dictionary<string, UndertaleGameObject> enemyDefs;
        public static Dictionary<string, UndertaleGameObject> hardmodeEnemyDefs;
        public static Dictionary<string, UndertaleGameObject> throwDefs;
        public static Random rand;
        //Used to generate the random value for the seed so it can be displayed
        public static Random seedGen;
        public static int seed;
        [STAThread]
        static void Main(string[] args)
        {
            seedGen = new Random();
            InitSettings();
            ReadData(dataWinPath);
            SetupWeights();
            ReadEnemyDefs();
            ReadThrowableDefs();
            SetBlockedEnemies();
            SetBlockedThrowables();
            RunRandomiser(outputPath);
            return;
        }

        //Initialise settings from settings.ini or create the file
        static void InitSettings()
        {
            try
            {
                if (File.Exists(SETTINGSPATH))
                {
                    string[] settings = File.ReadAllLines(SETTINGSPATH);
                    foreach (string setting in settings)
                    {
                        if (setting.StartsWith("data.win input path="))
                        {
                            dataWinPath = setting.Split('=')[1];
                        } else if (setting.StartsWith("data.win output path="))
                        {
                            outputPath = setting.Split('=')[1];
                        } else if (setting.StartsWith("Randomise Enemies (0/1)="))
                        {
                            if (!bool.TryParse(setting.Split('=')[1], out randomEnemies))
                            {
                                randomEnemies = true;
                            }
                        } else if (setting.StartsWith("Randomise Throwables (0/1)="))
                        {
                            if (!bool.TryParse(setting.Split('=')[1], out randomThrowables))
                            {
                                randomThrowables = true;
                            }
                        }
                    }
                    return;
                } else
                {
                    Console.WriteLine("Could not find settings.ini, running initial setup");
                    Console.WriteLine("data.win is normally located in your Katana ZERO directory");
                    Console.WriteLine("The output location will be overwritten, create a backup of the original first");
                    Console.WriteLine("Select the original unmodded data.win file");
                    StringBuilder sb = new StringBuilder();
                    sb.Append("data.win input path=");
                    dataWinPath = FileBrowser("Select input data.win");
                    sb.Append(dataWinPath);
                    Console.WriteLine("Select the target location for the new randomised data.win");
                    sb.Append("\ndata.win output path=");
                    outputPath = FileBrowser("Select output data.win location (WILL BE OVERWRITTEN, MAKE A BACKUP)");
                    sb.Append(outputPath);
                    sb.AppendLine("\nRandomise Enemies (0/1)=1");
                    sb.AppendLine("Randomise Throwables (0/1)=1");
                    File.WriteAllText(SETTINGSPATH, sb.ToString());
                    Console.WriteLine("Saved settings to settings.ini, you can edit them there later");
                }
            } catch (IOException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                Environment.Exit(1);
            }
        }

        //Browser for data.win location
        static string FileBrowser(string title)
        {
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                FileName = "data.win",
                DefaultExt = ".win"
            };
            fileDialog.Title = title;
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                return fileDialog.FileName;
            } else
            {
                Console.WriteLine("Invalid input file");
                Console.ReadLine();
                Environment.Exit(1);
                return "";
            }
        }

        //Runs the randomiser
        static void RunRandomiser(string outputPath)
        {
            Console.WriteLine("Enter new seed [leave blank for random] or Enter 'q' to exit");
            string choice = Console.ReadLine();
            if (choice != "q")
            {
                if (!int.TryParse(choice, out seed))
                {
                    seed = seedGen.Next();
                }
                rand = new Random(seed);
                Console.WriteLine("Using seed: " + seed.ToString());
            }
            else
            {
                return;
            }
            if (randomEnemies)
            {
                ReplaceEnemies();
            }
            if (randomThrowables)
            {
                ReplaceThrowables();
            }
            WriteData(outputPath);
            Console.WriteLine("Done");
            RunRandomiser(outputPath);
        }

        //Sets up the weights list
        static void SetupWeights()
        {
            weights = new Dictionary<string, int>
            {
                { "obj_enemy", 5 },
                { "obj_enemy_grunt", 5 },
                { "obj_enemy_shotgun", 5 },
                { "obj_enemy_shieldcop", 5 },
                { "obj_enemy_meele", 5 },
                { "obj_enemy_redpomp", 5 },
                { "obj_enemy_cop", 5 },
                { "obj_enemy_sci", 5 },
                { "obj_enemy_machinegun", 5 }
            };
            totalWeight = weights.Sum(x => x.Value);
        }
        
        //Populates enemyDefs with all valid enemy types in enemyNames
        static void ReadEnemyDefs()
        {
            Console.WriteLine("Reading enemy types");
            enemyDefs = new Dictionary<string, UndertaleGameObject>();
            hardmodeEnemyDefs = new Dictionary<string, UndertaleGameObject>();
            string compStr = "";
            foreach (var obj in data.GameObjects)
            {
                compStr = RemoveStrPrefix(obj.Name.Content, OBJPREFIX);
                if (enemyNames.Contains(compStr))
                {
                    if (!enemyDefs.ContainsKey(obj.Name.Content))
                    {
                        enemyDefs.Add(obj.Name.Content, obj);
                    }
                } else
                {
                    compStr = RemoveStrPrefix(obj.Name.Content, OBJPREFIX + HARDPREFIX);
                    if (enemyNames.Contains(compStr))
                    {
                        if (!hardmodeEnemyDefs.ContainsKey(obj.Name.Content))
                        {
                            hardmodeEnemyDefs.Add(obj.Name.Content, obj);
                        }
                    }
                }
            }
            return;
        }

        //Reads the types of throwables that are valid and in the throwNames list
        static void ReadThrowableDefs()
        {
            Console.WriteLine("Reading throwable types");
            throwDefs = new Dictionary<string, UndertaleGameObject>();
            foreach (var obj in data.GameObjects)
            {
                if (throwNames.Contains(obj.Name.Content))
                {
                    if (!throwDefs.ContainsKey(obj.Name.Content))
                    {
                        throwDefs.Add(obj.Name.Content, obj);
                    }
                }
            }
            return;
        }

        //Removes the prefix from the object string for comparison with enemyNames
        static string RemoveStrPrefix(string input, string toRemove)
        {
            string cleanStr = input;
            if (input.Contains(toRemove))
            {
                cleanStr = input.Remove(input.IndexOf(toRemove), toRemove.Length);
            }
            return cleanStr;
        }

        //Reads the data from data.win using UndertaleModLib
        static void ReadData(string dataWinPath) 
        {
            Console.WriteLine("Reading data.win");
            FileStream fs = new FileStream(dataWinPath, FileMode.Open, FileAccess.Read);
            try
            {
                data = UndertaleIO.Read(fs);
            } catch (IOException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                Environment.Exit(1);
            }
            fs.Close();
        }

        //Writes the data to a new data.win using UndertaleModLib
        static void WriteData(string outputPath)
        {
            Console.WriteLine("Writing randomised data.win");
            FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            try
            {
                UndertaleIO.Write(fs, data);
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                Environment.Exit(1);
            }
            fs.Close();
        }

        //Sets the enemies to be ignored with sufficient information to get the unique enemy (name, xpos, ypos)
        //These enemies usually have some special dialogue event that crashes if it uses the wrong enemy type
        static void SetBlockedEnemies()
        {
            blockedEnemies = new List<Pair<string, ObjDesc>>
            {
                new Pair<string, ObjDesc>("room_factory_0", new ObjDesc("obj_enemy", 480, 320)),
                new Pair<string, ObjDesc>("room_motel_4", new ObjDesc("obj_enemy_meele", 960, 352)),
                new Pair<string, ObjDesc>("room_haunted_alien2", new ObjDesc("obj_enemy", 864, 512)),
                new Pair<string, ObjDesc>("room_haunted_alien2", new ObjDesc("obj_enemy", 1152, 512)),
                new Pair<string, ObjDesc>("room_mansion_2", new ObjDesc("obj_enemy_meele", 860, 544)),
                new Pair<string, ObjDesc>("room_mansion_2", new ObjDesc("obj_enemy_shotgun", 954, 544)),
                new Pair<string, ObjDesc>("room_dragon_1", new ObjDesc("obj_enemy_shieldcop", 898, 640)),
                new Pair<string, ObjDesc>("room_dragon_1", new ObjDesc("obj_enemy_cop", 800, 640)),
                new Pair<string, ObjDesc>("room_dragon_1", new ObjDesc("obj_enemy_shotgun", 720, 640))
            };
        }

        //Sets the throwables to be ignored with sufficient information to get the unique throwable (name, xpos, ypos)
        //These throwables are usually required to complete the level
        static void SetBlockedThrowables()
        {
            blockedThrow = new List<Pair<string, ObjDesc>>
            {
                new Pair<string, ObjDesc>("room_haunted_silent1", new ObjDesc("obj_throwable_smoke_vial", 1616, 523)),
                new Pair<string, ObjDesc>("room_mansion_2", new ObjDesc("obj_throwable_explosive_vial", 2368, 848)),
            };
        }

        //Replaces the enemies with a random version provided that enemy is safe to replace
        static void ReplaceEnemies()
        {
            Console.WriteLine("Replacing enemies");
            var blockingList = new List<Pair<string, ObjDesc>>();
            foreach (UndertaleRoom room in data.Rooms)
            {
                if (!ignoredRooms.Contains(room.Name.Content))
                {
                    foreach (var obj in room.GameObjects)
                    {
                        if (enemyDefs.ContainsKey(obj.ObjectDefinition.Name.Content))
                        {
                            if (HasBlockedEnemyInRoom(room.Name.Content))
                            {
                                if (blockingList.Count == 0)
                                {
                                    blockingList = blockedEnemies.FindAll(s => s.First == room.Name.Content);
                                }
                                if (IsBlockedObject(blockingList, obj))
                                {
                                    continue;
                                }
                            }
                            if (stairRooms.Contains(room.Name.Content))
                            {
                                UndertaleGameObject chosen = ChooseEnemy();
                                if (chosen.Name.Content == "obj_enemy_sci")
                                {
                                    obj.ObjectDefinition = enemyDefs["obj_enemy_cop"];
                                } else
                                {
                                    obj.ObjectDefinition = chosen;
                                }
                            }
                            else
                            {
                                obj.ObjectDefinition = ChooseEnemy();
                            }
                        } else if (hardmodeEnemyDefs.ContainsKey(obj.ObjectDefinition.Name.Content))
                        {
                            obj.ObjectDefinition = ChooseEnemyHard();
                        }
                    }
                }
                blockingList.Clear();
            }
        }

        //Replaces the throwables with a random version provided that throwable is safe to replace
        static void ReplaceThrowables()
        {
            Console.WriteLine("Replacing throwables");
            var blockingList = new List<Pair<string, ObjDesc>>();
            foreach (UndertaleRoom room in data.Rooms)
            {
                if (!ignoredRooms.Contains(room.Name.Content))
                {
                    foreach (var obj in room.GameObjects)
                    {
                        if (throwDefs.ContainsKey(obj.ObjectDefinition.Name.Content))
                        {
                            if (HasBlockedThrowableInRoom(room.Name.Content))
                            {
                                if (blockingList.Count == 0)
                                {
                                    blockingList = blockedThrow.FindAll(s => s.First == room.Name.Content);
                                }
                                if (IsBlockedObject(blockingList, obj))
                                {
                                    continue;
                                }
                            }
                            obj.ObjectDefinition = ChooseThrowable();
                        }
                    }
                }
                blockingList.Clear();
            }
        }

        //Chooses the enemy to replace with
        static UndertaleGameObject ChooseEnemy()
        {
            int randVal = rand.Next(0, totalWeight);
            string choice = "";
            foreach (var weight in weights)
            {
                if (randVal < weight.Value)
                {
                    choice = weight.Key;
                    break;
                }
                randVal -= weight.Value;
            }
            if (enemyDefs.ContainsKey(choice))
            {
                return enemyDefs[choice];
            } else
            {
                return enemyDefs["obj_enemy_cop"];
            }
        }

        //Chooses a random enemy for a hardmode replacement, no weights are used
        static UndertaleGameObject ChooseEnemyHard()
        {
            int randVal = rand.Next(0, hardmodeEnemyDefs.Count);
            return hardmodeEnemyDefs.ElementAt(randVal).Value;
        }

        //Chooses a random throwables for a replacement, no weights are used
        static UndertaleGameObject ChooseThrowable()
        {
            int randVal = rand.Next(0, throwDefs.Count);
            return throwDefs.ElementAt(randVal).Value;
        }

        //Checks if the enemy is in the blocking list for that room
        static bool IsBlockedObject(List<Pair<string, ObjDesc>> blockingList, UndertaleRoom.GameObject obj)
        {
           if (blockingList.Exists(
                s => s.Second.Name == obj.ObjectDefinition.Name.Content &&
                s.Second.XPos == obj.X &&
                s.Second.YPos == obj.Y
           ))
           {
                return true; 
           }
            return false;
        }

        //Checks if there is any blocked enemy in that room
        static bool HasBlockedEnemyInRoom(string room)
        {
            var newList = blockedEnemies.FindAll(s => s.First == room);
            if (newList.Count > 0)
            {
                return true;
            }
            return false;
        }

        //Checks if there is any blocked enemy in that room
        static bool HasBlockedThrowableInRoom(string room)
        {
            var newList = blockedThrow.FindAll(s => s.First == room);
            if (newList.Count > 0)
            {
                return true;
            }
            return false;
        }
    }

    public class ObjDesc
    {
        public string Name { get; set; }
        public int XPos { get; set; }
        public int YPos { get; set; }
        
        public ObjDesc()
        {

        }
        
        public ObjDesc(string name, int xPos, int yPos)
        {
            Name = name;
            XPos = xPos;
            YPos = yPos;
        }
    }
}
