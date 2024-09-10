
using System;

namespace Jinroo;


public class GameplayPlayerData
{
  public int Index = -1;

  public Connection Connection;

  public PlayerState State;

  public GameObject Pawn;

  public PlayerController Controller;
}


public class GameplayStatics
{

  // private static Dictionary<Connection, int> PlayerIndexes = new();

  // private static Dictionary<int, PlayerController> PlayerControllers = new();

  // private static PlayerController LocalPlayerController;

  // private static Dictionary<int, GameObject> PlayerPawns = new();

  // private static int LocalPlayerIndex = -1;
  public static Dictionary<int, GameplayPlayerData> PlayersData = new();

  private static GameplayPlayerData LocalPlayerData;

  private static GameState GameState;

  private static GameMode GameMode;


  // NOTE: The gamemode exists only for the server so the host, return null for clients.
  public static GameMode GetGameMode()
  {
    return GameMode;
  }

  // NOTE: Only the Server can call this function, as there are no "Player" instance on the clients.
  public static Player GetServerPlayer( int playerIndex )
  {
    return GameMode.Players.Find( player => player.index == playerIndex );
  }

  public static Player GetServerPlayer( Connection connection )
  {
    var playerData = GetPlayerData( connection );

    if ( playerData is null || playerData.Index < 0 )
      return null;

    return GetServerPlayer( playerData.Index );
  }

  public static Player GetServerPlayer( PlayerController controller )
  {
    var playerData = GetPlayerData( controller );
    return GetServerPlayer( playerData.Index );
  }

  public static GameplayPlayerData GetPlayerData( int playerIndex )
  {
    return PlayersData[playerIndex];
  }

  public static GameplayPlayerData GetPlayerData( PlayerController playerController )
  {
    return PlayersData.Values.FirstOrDefault( data => data.Controller == playerController );
  }

  public static GameplayPlayerData GetPlayerData( GameObject playerPawn )
  {
    return PlayersData.Values.FirstOrDefault( data => data.Pawn == playerPawn );
  }

  public static GameplayPlayerData GetPlayerData( Connection connection )
  {
    return PlayersData.Values.FirstOrDefault( data => data.Connection == connection );
  }

  public static GameplayPlayerData GetLocalPlayerData()
  {
    return LocalPlayerData;
  }

  public static int GetPlayersCount()
  {
    return PlayersData.Count;
  }


  public static bool IsHost()
  {
    return Connection.Local.IsHost;
  }

  // public static PlayerController GetPlayerController( int playerIndex )
  // {
  //   return PlayerControllers[playerIndex];
  // }

  // public static PlayerController GetLocalPlayerController()
  // {
  //   return LocalPlayerController;
  // }

  // public static int GetPlayerIndex( Connection connection )
  // {

  //   return PlayerIndexes[connection];
  // }

  public static int GetPlayerIndex( GameObject playerPawn )
  {
    return 0;
  }


  // public static Connection GetPlayerConnection( int playerIndex )
  // {
  //   return PlayerIndexes.FirstOrDefault( x => x.Value == playerIndex ).Key;
  // }

  // public static bool IsPlayerIndex( int playerIndex, Connection connection )
  // {
  //   var connectionIndex = GetPlayerIndex( connection );
  //   return connectionIndex == playerIndex;
  // }

  // public static int GetLocalPlayerIndex()
  // {
  //   return LocalPlayerIndex;
  // }


  // public static void __SetPlayerIndex( Connection connection, int playerIndex )
  // {
  //   if ( PlayerIndexes.ContainsKey( connection ) )
  //     return;


  //   PlayerIndexes.Add( connection, playerIndex );

  //   if ( Connection.Local.Id == connection.Id )
  //   {
  //     LocalPlayerIndex = playerIndex;
  //   }
  // }

  public static void __SetPlayerConnection( int playerIndex, Connection connection )
  {

    if ( PlayersData.ContainsKey( playerIndex ) )
    {
      PlayersData[playerIndex].Index = playerIndex;
      PlayersData[playerIndex].Connection = connection;
    }
    else
    {
      GameplayPlayerData data = new GameplayPlayerData { Index = playerIndex, Connection = connection };
      PlayersData.Add( playerIndex, data );
    }
  }

  public static void __SetPlayerController( int playerIndex, PlayerController playerController )
  {
    // PlayerControllers.Add( playerIndex, playerController );


    if ( PlayersData.ContainsKey( playerIndex ) )
    {
      PlayersData[playerIndex].Index = playerIndex;
      PlayersData[playerIndex].Controller = playerController;
    }
    else
    {
      GameplayPlayerData data = new GameplayPlayerData { Index = playerIndex, Controller = playerController };
      PlayersData.Add( playerIndex, data );
    }
  }

  public static void __SetPlayerState( int playerIndex, PlayerState playerState )
  {
    // PlayerControllers.Add( playerIndex, playerController );


    if ( PlayersData.ContainsKey( playerIndex ) )
    {
      PlayersData[playerIndex].Index = playerIndex;
      PlayersData[playerIndex].State = playerState;
    }
    else
    {
      GameplayPlayerData data = new GameplayPlayerData { Index = playerIndex, State = playerState };
      PlayersData.Add( playerIndex, data );
    }
  }

  public static void __SetLocalPlayerController( PlayerController playerController )
  {
    LocalPlayerData = GetPlayerData( playerController );
  }

  public static void __SetPlayerPawn( int playerIndex, GameObject playerPawn )
  {
    // PlayerPawns.Add( playerIndex, playerPawn );


    if ( PlayersData.ContainsKey( playerIndex ) )
    {
      PlayersData[playerIndex].Index = playerIndex;
      PlayersData[playerIndex].Pawn = playerPawn;
    }
    else
    {
      GameplayPlayerData data = new GameplayPlayerData { Index = playerIndex, Pawn = playerPawn };
      PlayersData.Add( playerIndex, data );
    }
  }

  public static void __SetGameMode( GameMode gm )
  {
    GameMode = gm;
  }


  public static void __Reset()
  {
    // PlayerIndexes.Clear();
    // PlayerControllers.Clear();
    // PlayerPawns.Clear();
    PlayersData.Clear();

    LocalPlayerData = default;
  }

  private static List<ARole> RegisteredRoles = new() {
    new VillagersRole(),
    new CupidRole(),
    new HunterRole(),
    new SeerRole(),
    new WereWolfRole(),
    new WitchRole(),
  };

  public static ARole GetRegisteredRole( string key )
  {
    return RegisteredRoles.Find( role => role.GetKey() == key );
  }

  public static List<ARole> GetRegisteredRoles()
  {
    return RegisteredRoles;
  }

  public static Type GetRoleClass( string key )
  {
    return GetRegisteredRole( key )?.GetType();
  }

  public static ARole CreateRole( string key )
  {
    switch ( key )
    {
      case VillagersRole.Key:
        return new VillagersRole();
      case CupidRole.Key:
        return new CupidRole();
      case HunterRole.Key:
        return new HunterRole();
      case WitchRole.Key:
        return new WitchRole();
      case SeerRole.Key:
        return new SeerRole();
      case WereWolfRole.Key:
        return new WereWolfRole();
      default:
        return null;
    }
  }

  public static void FixRolesSetup( ref Dictionary<string, int> roles, int playerCount )
  {
    int rolesAmount = roles.Values.Aggregate( 0, ( current, next ) => current + next );

    // We are allowed to have more roles than the player count.
    if ( rolesAmount >= playerCount )
      return;

    // In case there are less roles than player count, we need to fix it.
    // We use the villagers role for fill. 
    if ( roles.ContainsKey( VillagersRole.Key ) )
    {
      var amount = roles[VillagersRole.Key];

      roles[VillagersRole.Key] = amount + (playerCount - rolesAmount);
    }
    else
    {
      roles[VillagersRole.Key] = playerCount - rolesAmount;
    }
  }

  public static void DisplayRegisterRole()
  {
    foreach ( var role in RegisteredRoles )
    {
      Log.Info( role.GetKey() );
    }
  }
}

