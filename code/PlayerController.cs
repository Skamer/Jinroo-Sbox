using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.UI;


namespace Jinroo;

public class PlayerController : Component
{

  public GameObject OwningPawn { set; get; }

  public GameObject VoteCollider;

  public static PlayerController Local { set; get; }
  public GameObject Interface;

  public GameHUD GameHUD;

  public ChoicesPanel ChoicesPanel;

  public GameChat Chat;

  CameraComponent Camera;

  GameObject NightFog;

  public ARole Role { set; get; }

  [Sync] public Angles EyeAngles { get; set; } = new();

  public List<GameplayPlayerData> Targets = new();

  public int MaxTargets = 0;

  public bool CanChangeTargets = false;


  private GameObject PawnHovered;

  public static PlayerController Create( Connection owner )
  {
    var holder = new GameObject();
    var pc = holder.Components.Create<PlayerController>();
    holder.Name = $"PlayerController {owner.DisplayName}";
    holder.NetworkSpawn( owner );

    return pc;
  }

  protected override void OnAwake()
  {
    base.OnAwake();

    //IMPORTANT ! Be sure in the scene, the name is well "NightFog" for avoiding big problems
    NightFog = Scene.Directory.FindByName( "NightFog" )?.FirstOrDefault();

    // IMPORTANT ! Be sure in the scene, the name is well "Interface" for avoiding big problems
    Interface = Scene.Directory.FindByName( "Interface" )?.FirstOrDefault();

    GameHUD = Interface.Components.Get<GameHUD>( true );
    ChoicesPanel = Interface.Components.Get<ChoicesPanel>( true );
  }

  protected override void OnFixedUpdate()
  {
    base.OnFixedUpdate();

    OnRotatePawn();

    if ( IsProxy )
      return;

    if ( Input.Pressed( "Interact" ) )
      Interact();
  }

  protected override void OnUpdate()
  {
    base.OnUpdate();

    if ( IsProxy )
      return;

    EyeAngles += Input.AnalogLook;

    var eyeAngles = EyeAngles.WithRoll( 0 ).WithPitch( EyeAngles.pitch.Clamp( -90.0f, 90.0f ) ).WithYaw( 0 );

    if ( Camera is not null )
    {
      Camera.Transform.LocalRotation = eyeAngles.ToRotation();
    }
  }

  protected override void OnPreRender()
  {
    base.OnPreRender();

    if ( IsProxy )
      return;

    if ( Camera is null )
      return;

    var trace = Scene.Trace.Ray( new Ray( Camera.Transform.Position, Camera.Transform.Rotation.Forward ), 1000 ).WithTag( "player" );

    if ( VoteCollider is not null )
      trace = trace.IgnoreGameObject( VoteCollider );

    var tr = trace.Run();

    if ( tr.Hit )
    {
      var pawn = tr.GameObject.Parent;

      if ( pawn != PawnHovered )
        HighlightPawn( pawn );

      PawnHovered = pawn;
    }
    else
    {
      if ( PawnHovered is not null )
        HighlightPawn();

      PawnHovered = null;
    }
  }

  protected void OnRotatePawn()
  {
    if ( !Connection.Local.IsHost )
      return;

    if ( OwningPawn is not null )
    {
      OwningPawn.Transform.LocalRotation = EyeAngles.WithPitch( 0 ).WithRoll( 0 ).ToRotation();
    }
  }

  [Broadcast( NetPermission.OwnerOnly )]
  protected void Server_OnRotatePawn( Angles eyeAngle )
  {
    // if ( !GameplayStatics.IsPlayerIndex( 0, Rpc.Caller ) )
    //   return;

    if ( !Connection.Local.IsHost )
      return;

    OwningPawn.Transform.LocalRotation = EyeAngles.WithPitch( 0 ).WithRoll( 0 ).ToRotation();
  }

  private void HighlightPawn( GameObject pawn = null )
  {
    if ( PawnHovered is not null )
    {
      var outline = PawnHovered.Components.Get<HighlightOutline>( FindMode.EverythingInChildren );
      if ( outline is not null )
      {
        outline.Enabled = false;
      }
    }

    if ( pawn is not null )
    {
      var outline = pawn.Components.Get<HighlightOutline>( FindMode.EverythingInChildren );
      if ( outline is not null )
      {
        outline.Enabled = true;
      }
    }
  }

  public void Interact()
  {
    // if ( Targets.Count == MaxTargets && !CanChangeTargets )
    //   return;

    if ( PawnHovered is null )
      return;

    GameplayPlayerData player = GameplayStatics.GetPlayerData( PawnHovered );

    if ( player is null )
      return;

    // var alreadyTargeted = Targets.Contains( player );

    // if ( alreadyTargeted && CanChangeTargets )
    //   Targets.Remove( player );
    // else
    //   Targets.Add( player );
    InteractPlayer( player );

  }

  public void InteractPlayer( GameplayPlayerData player )
  {
    InteractVote( player );
    InteractTargets( player );
  }

  public void InteractVote( GameplayPlayerData player )
  {
    if ( !IsVoting || VoteChoices is null )
      return;

    if ( VoteChoices.Count > 0 && !VoteChoices.Contains( player.Index ) )
      return;

    Server_VotePlayer( player.Index );
  }

  public void InteractTargets( GameplayPlayerData player )
  {
    if ( !IsWaitingTargetsRunning() )
      return;

    var alreadyTargeted = Targets.Contains( player );

    if ( alreadyTargeted )
    {
      if ( CanChangeTargets )
        Targets.Remove( player );
    }
    else
    {
      Targets.Add( player );
    }
  }

  public void PossessPawn( GameObject pawn )
  {
    OwningPawn = pawn;

    if ( OwningPawn is null )
      return;

    var model = OwningPawn.Components.Get<ModelRenderer>( FindMode.InDescendants );
    if ( model is not null )
      model.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;


    // Remove the priority on the previous camera if needed
    if ( Camera is not null )
    {
      Camera.Priority = 1;
      Camera.IsMainCamera = false;
    }

    var camera = OwningPawn.Components.Get<CameraComponent>( FindMode.EverythingInChildren );

    if ( camera is not null )
    {
      camera.IsMainCamera = true;
      camera.Priority = 100;
    }

    Camera = camera;

    VoteCollider = OwningPawn.Children.Find( child => child.Name == "VoteCollider" );
  }

  [Authority( NetPermission.HostOnly )]
  public void Client_PossessPawn( Guid pawnGuid )
  {

    if ( IsProxy )
      return;

    OwningPawn = Scene.Directory.FindByGuid( pawnGuid );

    PossessPawn( OwningPawn );
  }

  [Authority( NetPermission.HostOnly )]
  public void Client_Init()
  {
    if ( IsProxy )
      return;

    Local = this;

    GameplayStatics.__SetLocalPlayerController( this );
  }

  [Authority( NetPermission.HostOnly )]
  public void Client_AssignRole( string role )
  {
    if ( IsProxy )
      return;

    Role = GameplayStatics.CreateRole( role );

    GameHUD.Role = Role;
  }

  [Authority( NetPermission.HostOnly )]
  public void Client_OnTask( Dictionary<string, object> data )
  {
    if ( IsProxy )
      return;

    // var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>( json );

    _ = Role?.Client_OnTask( this, data, Task );

  }

  [Authority( NetPermission.HostOnly )]
  public void Client_OnTaskRequest( Dictionary<string, object> requestData, Guid token )
  {
    if ( IsProxy )
      return;

    _ = Role?.Client_OnTaskRequest( this, requestData, Task, token );
  }

  [Broadcast( NetPermission.OwnerOnly )]
  public void Server_OnTaskResponse( Dictionary<string, object> responseData, Guid token )
  {
    if ( !GameplayStatics.IsHost() )
      return;

    if ( token == Guid.Empty )
      return;

    var playerData = GameplayStatics.GetPlayerData( this );
    var gameMode = GameplayStatics.GetGameMode();

    var player = gameMode.Players.Find( player => player.index == playerData.Index );

    gameMode.CurrentRoleInProcess?.OnTaskResponse( responseData, token );

    // gameMode.CurrentRoleInProcess?.

    // Get the GameMode
    // 
  }

  public void AddChoice( string id, string label, string icon = "", string color = "" )
  {
    if ( ChoicesPanel is null )
      return;

    ChoicesPanel.AddChoice( id, label, icon, color );
  }

  public void AddChoice( string id, string label, string icon = "", ChoiceButton.StyleProps styles = new() )
  {
    if ( ChoicesPanel is null )
      return;


    ChoicesPanel.AddChoice( id, label, icon, styles );
  }

  public void SetOnConfirmedChoice( Action<string> callback )
  {
    if ( ChoicesPanel is null )
      return;

    ChoicesPanel.OnConfirmedChoice = callback;
  }

  public void SetChoicesPanelVisible( bool isVisible )
  {
    if ( ChoicesPanel is null )
      return;

    ChoicesPanel.Enabled = isVisible;
  }

  public void SetChoicesDescription( string desc = "" )
  {
    if ( ChoicesPanel is null )
      return;

    ChoicesPanel.SetDescription( desc );
  }

  public void SetChoicesDescription( Sandbox.Razor.RenderFragment desc )
  {
    if ( ChoicesPanel is null )
      return;

    ChoicesPanel.SetDescription( desc );
  }


  public void SetChoicesTitle( string title = "" )
  {
    if ( ChoicesPanel is null )
      return;

    ChoicesPanel.Title = title;

  }
  public void SetChoicesTitleIcon( string icon = "" )
  {
    if ( ChoicesPanel is null )
      return;

    ChoicesPanel.TitleIcon = icon;

  }


  public bool IsChoicesTimeout( float timeoutTime = 0 )
  {
    if ( timeoutTime > 0 && RealTime.Now >= timeoutTime )
      return true;

    return false;
  }

  public async Task<string> WaitingChoices( float timeoutTime = 0 )
  {
    string choice = "";
    ChoicesPanel.OnConfirmedChoice = ( c ) => choice = c;

    while ( choice == "" && !IsChoicesTimeout( timeoutTime ) )
    {
      await Task.Frame();
    }

    ChoicesPanel.ClearChoices();

    return choice;
  }

  private float WaitingTimeoutTime;

  private bool IsWaitingTargetsRunning()
  {
    // If we cannot change the targets, and we reach the max targets.
    if ( !CanChangeTargets && MaxTargets == Targets.Count )
      return false;

    // if we have timeout time, and the player elapsed it, stop here even if no all targets has been choosen
    // the next time the player need to be faster
    if ( WaitingTimeoutTime > 0 && RealTime.Now > WaitingTimeoutTime )
      return false;

    // If we reach the max targets, we could stop here. 
    if ( MaxTargets == Targets.Count )
      return false;

    return true;
  }

  public async Task<List<GameplayPlayerData>> WaitingTargets( int maxTarget = 1, bool canChangeTargets = false, float timeoutTime = 0 )
  {
    Targets.Clear();
    MaxTargets = maxTarget;
    CanChangeTargets = canChangeTargets;
    WaitingTimeoutTime = timeoutTime;

    while ( IsWaitingTargetsRunning() )
    {
      await Task.Frame();
    }

    return Targets;
  }

  [Broadcast( NetPermission.OwnerOnly )]
  public void Server_SendChatMessage( string message, GameChannels channel = GameChannels.GLOBAL )
  {

    if ( !GameplayStatics.IsHost() )
      return;


    var author = GameplayStatics.GetServerPlayer( Rpc.Caller );

    if ( author is null )
      return;

    author.SendChatMessage( message, channel );
  }


  [Authority( NetPermission.HostOnly )]
  public void Client_SendChatMessage( string message, string author, GameChannels channel )
  {
    if ( IsProxy )
      return;

    GameHUD.Chat?.AddLocalChatMessage( author, message, channel );
  }

  [Authority( NetPermission.HostOnly )]
  public void Client_SendServerMessage( string message, ServerMessageType type = ServerMessageType.INFO, GameChannels channel = GameChannels.GLOBAL )
  {
    if ( IsProxy )
      return;

    GameHUD.Chat?.AddLocalServerMessage( message, type, channel );
  }


  // VOTE SYSTEM
  private bool IsVoting = false;

  private List<int> VoteChoices;

  [Broadcast( NetPermission.OwnerOnly )]
  public void Server_VotePlayer( int choice )
  {
    if ( !GameplayStatics.IsHost() )
      return;

    var player = GameplayStatics.GetServerPlayer( this );

    var chosenPlayer = GameplayStatics.GetServerPlayer( choice );


    player.VotePlayer( chosenPlayer );
    // player.VotePlayer(playerChoice)


    // var playerData = GameplayStatics.GetPlayerData( this );
    // var gameMode = GameplayStatics.GetGameMode();

    // var player = gameMode.Players.Find( player => player.index == playerData.Index );


    // GameplayerStatics.GetServerPlayer()

  }

  [Authority( NetPermission.HostOnly )]
  public void Client_OnStartVote( List<int> participants, List<int> choices, float timeout )
  {
    if ( IsProxy )
      return;

    IsVoting = true;
    VoteChoices = choices;
    // WaitingTargets(1, true, timeout, choices,
  }

  [Authority( NetPermission.HostOnly )]
  public void Client_OnEndVote()
  {
    if ( IsProxy )
      return;

    IsVoting = false;

    var playerCount = GameplayStatics.GetPlayersCount();
    for ( int i = 0; i < playerCount; i++ )
    {
      var pawn = GameplayStatics.GetPlayerData( i )?.Pawn;
      if ( pawn is null )
        continue;

      var nameplate = pawn.Components.GetAll<Nameplate>( FindMode.EverythingInDescendants ).FirstOrDefault();

      if ( nameplate is null )
        continue;

      nameplate.VoteCount = 0;
      nameplate.HasVotedHim = false;
    }
  }

  [Authority( NetPermission.HostOnly )]
  public void Client_OnPlayerReceivedVoteChanged( int targetedPlayerIndex, List<int> votingPlayersIndex )
  {
    var targetedPlayerData = GameplayStatics.GetPlayerData( targetedPlayerIndex );
    var pawn = targetedPlayerData.Pawn;

    if ( pawn is null )
      return;

    var nameplate = pawn.Components.GetAll<Nameplate>( FindMode.EverythingInDescendants ).FirstOrDefault();

    if ( nameplate is null )
      return;

    var myIndex = GameplayStatics.GetLocalPlayerData().Index;

    nameplate.VoteCount = votingPlayersIndex.Count;
    nameplate.HasVotedHim = votingPlayersIndex.Contains( myIndex );
  }

  [Authority( NetPermission.HostOnly )]
  public void Client_RestrictVision()
  {
    if ( IsProxy )
      return;

    NightFog.Enabled = true;

    var sun = Scene.GetAllComponents<DirectionalLight>()?.FirstOrDefault();

    if ( sun is not null )
    {
      sun.SkyColor = Color.FromRgb( 0x040404 );
      sun.LightColor = Color.FromRgb( 0x000000 );
    }

    if ( Camera is not null && !Camera.RenderExcludeTags.Has( "player" ) )
      Camera.RenderExcludeTags.Add( "player" );
  }

  [Authority( NetPermission.HostOnly )]
  public void Client_GiveVision()
  {
    if ( IsProxy )
      return;

    NightFog.Enabled = false;

    var sun = Scene.GetAllComponents<DirectionalLight>()?.FirstOrDefault();

    if ( sun is not null )
    {
      sun.SkyColor = Color.FromRgb( 0xffffff );
      sun.LightColor = Color.FromRgb( 0xffffff );
    }

    if ( Camera is not null && Camera.RenderExcludeTags.Has( "player" ) )
      Camera.RenderExcludeTags.Remove( "player" );
  }

  [Authority( NetPermission.HostOnly )]
  public void Client_SetRoleType( Dictionary<int, RoleType> playersRoleType )
  {
    if ( IsProxy )
      return;

    foreach ( KeyValuePair<int, RoleType> kvp in playersRoleType )
    {
      var playerData = GameplayStatics.GetPlayerData( kvp.Key );

      if ( playerData.Pawn is null )
        continue;

      // Update the nameplate 
      var nameplate = playerData.Pawn.Components.GetAll<Nameplate>( FindMode.EverythingInDescendants ).FirstOrDefault();

      if ( nameplate is not null )
        nameplate.RType = kvp.Value;
    }
  }

  [Authority( NetPermission.HostOnly )]
  public void Client_SetLovers( List<int> lovers )
  {
    if ( IsProxy )
      return;

    foreach ( var loverIndex in lovers )
    {
      var playerData = GameplayStatics.GetPlayerData( loverIndex );

      if ( playerData.Pawn is null )
        continue;

      // Update the nameplate 
      var nameplate = playerData.Pawn.Components.GetAll<Nameplate>( FindMode.EverythingInDescendants ).FirstOrDefault();

      if ( nameplate is not null )
        nameplate.IsLover = true;
    }
  }

  public void OnChatMessageEntered( string message, GameChannels channel )
  {
    Server_SendChatMessage( message, channel );
  }

  public void Test()
  {
    var panel = new Panel();
    panel.ScrollOffset = new Vector2( 0, 200 );
    // panel.TryScrollToBottom
    // panel.HasScrollY
    // panel.ScrollSize
    // panel.HasTooltip
    // panel.HasTooltip
    // panel.HasScrollY

    //panel.TryScrollToBottom
    // panel.HasMouseCapture
    //
  }
}