using System;

namespace Jinroo;

public class GameState : Component
{

  private static GameState _gs;

  public List<PlayerState> Players = new();

  public GamePhaseType Phase = GamePhaseType.UNKNOWN;

  public GameDayTime DayTime = GameDayTime.NIGHT;

  public float PhaseTimeoutTime = 0;
  static SoundHandle NightAmbientMusic;


  public static GameState Create()
  {
    var holder = new GameObject();
    var gs = holder.Components.Create<GameState>();
    holder.Name = "GameState";
    holder.NetworkSpawn();

    return gs;
  }

  public static GameState Get()
  {
    return _gs;
  }


  protected override void OnAwake()
  {
    _gs = this;

    base.OnAwake();
  }

  protected override void OnDestroy()
  {
    _gs = this;

    base.OnDestroy();
  }


  // To Delete

  [Broadcast( NetPermission.HostOnly )]
  public void Multicast_AddPlayerState( Guid guid )
  {
    var obj = Scene.Directory.FindByGuid( guid );
    var ps = obj.Components.Get<PlayerState>();

    Players.Add( ps );
  }

  public void AddPlayerState( PlayerState playerState )
  {
    Players.Add( playerState );
  }


  [Broadcast( NetPermission.HostOnly )]
  public void Multicast_SetPhase( GamePhaseType phase, float timeout )

  {
    Phase = phase;
    PhaseTimeoutTime = RealTime.Now + timeout;

    var controller = GameplayStatics.GetLocalPlayerData()?.Controller;

    if ( controller is null && controller.GameHUD is null )
      return;

    controller.GameHUD.Phase = Phase;
    controller.GameHUD.PhaseTimeoutTime = PhaseTimeoutTime;
  }

  [Broadcast( NetPermission.HostOnly )]
  public void Multicast_SetPhaseTimeout( float timeout )
  {
    var controller = GameplayStatics.GetLocalPlayerData()?.Controller;
    PhaseTimeoutTime = RealTime.Now + timeout;

    if ( controller is null && controller.GameHUD is null )
      return;

    controller.GameHUD.PhaseTimeoutTime = PhaseTimeoutTime;
  }

  [Broadcast( NetPermission.HostOnly )]
  public void Multicast_SetDayTime( GameDayTime dayTime )
  {
    DayTime = dayTime;

    var skybox = Scene.GetAllComponents<SkyBox2D>()?.FirstOrDefault();
    var sun = Scene.GetAllComponents<DirectionalLight>()?.FirstOrDefault();

    if ( skybox is null || sun is null )
      return;

    if ( dayTime == GameDayTime.DAY )
    {
      // Sound.StopAll( 0 );
      skybox.Tint = Color.FromRgb( 0xffffff );
      sun.SkyColor = Color.FromRgb( 0xffffff );
    }
    else
    {
      // Sound.Play( AmbientNightSound );
      skybox.Tint = Color.FromRgb( 0x040404 );
      sun.SkyColor = Color.FromRgb( 0x040404 );
    }
  }

  [Broadcast( NetPermission.HostOnly )]
  public void Multicast_OnBeginNight()
  {
    // Sound.Play( "sounds/night_ambient.sound" );
    // AmbientNightSound
    NightAmbientMusic = Sound.Play( "sounds/night_ambient.sound" );
  }

  [Broadcast( NetPermission.HostOnly )]
  public void Multicast_OnBeginDay()
  {
    NightAmbientMusic?.Stop( 5f );
  }


  [Broadcast( NetPermission.HostOnly )]
  public void Multicast_OnPlayerDead( int playerIndex )
  {
    // Sound.Play( "sounds/player_kill.sound" );


    var playerData = GameplayStatics.GetPlayerData( playerIndex );

    if ( playerData is null )
      return;

    var pawn = playerData.Pawn;

    foreach ( var child in pawn.Children )
    {
      if ( child.Name != "Camera" )
        child.Enabled = false;
    }

    var cameraColorAdjust = pawn.Components.GetInDescendants<ColorAdjustments>( true );
    if ( cameraColorAdjust is not null )
    {
      cameraColorAdjust.Enabled = true;
    }


    Sound.Play( "sounds/player_death.sound", pawn.Transform.Position );
  }

  [Broadcast( NetPermission.HostOnly )]
  public void Multicast_UpdateRoleComposition( Dictionary<string, int> RolesAmount )
  {
    var controller = GameplayStatics.GetLocalPlayerData()?.Controller;

    if ( controller is null )
      return;

    var gameHUD = controller.GameHUD;
    gameHUD.RolesAmount = RolesAmount;

  }

  [Broadcast( NetPermission.HostOnly )]
  public void Multicast_SendServerMessage( string message, ServerMessageType type = ServerMessageType.INFO )
  {
    var controller = GameplayStatics.GetLocalPlayerData()?.Controller;

    if ( controller is null )
      return;

    controller.GameHUD.Chat?.AddLocalServerMessage( message, type, GameChannels.GLOBAL );
  }

  [Broadcast( NetPermission.HostOnly )]
  public void Multicast_OnEndGame( RoleTeam winnerTeam, List<int> winners, Dictionary<int, string> playersRoles, string winnerText )
  {
    var playerData = GameplayStatics.GetLocalPlayerData();
    var controller = playerData.Controller;

    if ( controller is null )
      return;

    var gameHUD = controller.GameHUD;

    gameHUD.Winners = winners;
    gameHUD.WinnerTeam = winnerTeam;
    gameHUD.WinnerText = winnerText;
    gameHUD.PlayersRoles = playersRoles;
    gameHUD.IsWin = winners.Contains( playerData.Index );
    gameHUD.IsEndGame = true;
  }
}

// Role.d

