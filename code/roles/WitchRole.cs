using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Text.Json;
using System.Web;
using System.Text.Encodings.Web;
using Sandbox.Razor;




namespace Jinroo;

public class WitchRole : ARole
{
  public WitchRole()
  {
    Team = RoleTeam.VILLAGE;
    Type = RoleType.VILLAGER;
  }

  public const string Key = "witch";

  public override string GetKey() => Key;


  public override int GetOrder() => (int)RoleOrder.WITCH;


  public override string GetName()
  {
    return "Witch";
  }
  public override string GetIcon() => "/textures/roles/card_role_witch.svg";

  public static string GetLifeFlaskIcon() => "/textures/abilities/witch_life_flask.svg";

  public static string GetDeathFlaskIcon() => "/textures/abilities/witch_death_flask.svg";


  public override string GetBroadcastTaskText()
  {
    return "The witch is crafting flask.";
  }

  public override string GetAbilityText()
  {
    return "Each night, the witch can decide to use a life potion for saving someone of werewolves or a death potion for killing someone.";
  }


  private bool HasDeathFlask = true;
  private bool HasLifeFlask = true;

  public static readonly string ActionKey = "action";
  public static readonly string HasDeathFlaskKey = "has_death_flask";
  public static readonly string HasLifeFlaskKey = "has_life_flask";
  public static readonly string TargetKey = "target";

  public static readonly string VictimKey = "victim";

  public static readonly string DeathFlaskAction = "death_flask_action";
  public static readonly string LifeFlaskAction = "life_flask_action";



  public override async Task OnNightTurn( TaskSource task )
  {
    if ( Player is null || !Player.IsAlive )
      return;

    Player.GiveVision();

    GameMode.GameState.Multicast_SetPhase( GamePhaseType.WITCH, GetTimeout() );
    GameMode.GameState.Multicast_SendServerMessage( GetBroadcastTaskText() );

    var requestData = new Dictionary<string, object>();
    requestData.Add( HasDeathFlaskKey, HasDeathFlask );
    requestData.Add( HasLifeFlaskKey, HasLifeFlask );


    Player victim;

    try
    {
      victim = GameMode.PendingKills.Find( player => player.DeathReasons.Contains( KillReason.WEREWOLF ) );

      if ( victim is not null )
        requestData.Add( VictimKey, victim.index );
    }
    finally { }


    // string json = JsonSerializer.Serialize( data );

    // In some case the player can have invalid controller, for example when the player is disconnected.
    // so we need to check it
    // Player.Controller?.Client_OnTask( data );


    // Client_OnRoleTask()

    // await RequestTask

    var responseData = await RequestTask( task, requestData, RealTime.Now + GetTimeout() );
    // await task.Delay( 10000 );

    if ( !HasDeathFlask && !HasLifeFlask )
      return;

    // We don't continue if it's a bot 
    if ( Player.Controller is null )
      return;

    if ( !responseData.ContainsKey( ActionKey ) )
      return;

    var action = responseData[ActionKey] as string;
    if ( action is null )
      return;

    if ( action != DeathFlaskAction && action != LifeFlaskAction )
      return;

    if ( action == DeathFlaskAction && HasDeathFlask )
    {

      if ( !responseData.ContainsKey( TargetKey ) )
        return;

      var targetIndex = (int)responseData[TargetKey];

      var target = GameMode.Players[targetIndex];

      if ( target is null )
        return;

      HasDeathFlask = false;

      target.TryKill( KillReason.WITCH );
    }
    else if ( action == LifeFlaskAction && HasLifeFlask )
    {
      if ( victim is null )
        return;

      HasLifeFlask = false;

      victim.Save( KillReason.WEREWOLF );
    }
  }

  // public void OnTaskResponse( Dictionary<string, object> responseData )
  // {

  // }

  // public override void OnExperiment()
  // {
  //   Log.Warning( "La sorciere experimente" );
  // }


  // public override async Task Client_OnTask( PlayerController controller, Dictionary<string, object> data, TaskSource task )
  // {
  //   var hasDeathFlask = (bool)data[HasDeathFlaskKey];
  //   var hasLiveFlask = (bool)data[HasLifeFlaskKey];

  //   Log.Warning( $"In Client Task {hasDeathFlask} / {hasLiveFlask} / {controller.GameHUD}" );

  //   controller.AddChoice( "death_flask", "Use the death flask", "icon:check", "green" );
  //   controller.AddChoice( "healing_flask", "Use the healing flask", "icon:local_hospital", "green" );
  //   controller.AddChoice( "nothing", "Does nothing", "icon:logout", "red" );
  //   controller.SetChoicesPanelVisible( true );

  //   var choice = await controller.WaitingChoices();
  //   controller.SetChoicesPanelVisible( false );

  //   Log.Error( $"The choice is {choice}" );

  //   var targets = await controller.WaitingTargets();

  //   foreach ( var target in targets )
  //   {
  //     Log.Warning( $"Target {target.Index}" );
  //   }

  //   Dictionary<string, object> responseData = new()
  //   {
  //     { "targets", targets.Select( player => player.Index ).ToList() },
  //     { "flask", choice}
  //   };

  //   //  controller.Server_OnTaskResponse( responseData );

  // }

  // T
  public override async Task Client_OnTaskRequest( PlayerController controller, Dictionary<string, object> requestData, TaskSource task, Guid token )
  {
    var timeoutTime = RealTime.Now + GetTimeout();
    var hasDeathFlask = (bool)requestData[HasDeathFlaskKey];
    var hasLifeFlask = (bool)requestData[HasLifeFlaskKey];

    GameplayPlayerData playerData = null;

    if ( requestData.ContainsKey( VictimKey ) )
    {
      var playerIndex = (int)requestData[VictimKey];
      playerData = GameplayStatics.GetPlayerData( playerIndex );
    }

    controller.SetChoicesTitle( "Que faire ?" );
    controller.SetChoicesTitleIcon( GetIcon() );
    controller.SetChoicesDescription( CreateDescFragment( playerData?.State?.Name ) );

    if ( hasDeathFlask )
      AddDeathFlaskAction( controller );

    if ( hasLifeFlask )
      AddLifeFlaskAction( controller, playerData?.State?.Name );

    AddNothingAction( controller );

    controller.SetChoicesPanelVisible( true );

    var choice = await controller.WaitingChoices( timeoutTime );
    controller.SetChoicesPanelVisible( false );


    if ( choice == "" || choice == "nothing" )
    {
      controller.Server_OnTaskResponse( new(), token );
      return;
    }

    Dictionary<string, object> responseData = new();
    responseData.Add( ActionKey, choice );

    if ( choice == DeathFlaskAction )
    {
      var targets = await controller.WaitingTargets( 1, false, timeoutTime );

      if ( targets.Count == 1 )
        responseData.Add( TargetKey, targets.First().Index );
    }

    controller.Server_OnTaskResponse( responseData, token );
  }

  public static void AddLifeFlaskAction( PlayerController controller, string victim = "" )
  {
    ChoiceButton.StyleProps styles = new() { BackgroundColor = "green" };

    controller.AddChoice( LifeFlaskAction, $"Save {victim} in using your Life Flask", GetLifeFlaskIcon(), styles );
  }

  public static void AddDeathFlaskAction( PlayerController controller )
  {
    ChoiceButton.StyleProps styles = new() { BackgroundColor = "red" };

    controller.AddChoice( DeathFlaskAction, "Use your Death Flask for killing a player", GetDeathFlaskIcon(), styles );
  }

  public static void AddNothingAction( PlayerController controller )
  {
    ChoiceButton.StyleProps styles = new();

    controller.AddChoice( "nothing", "Do nothing !!!", "icon:logout", styles );
  }

  public static RenderFragment CreateDescFragment( string target )
  {
    return new( ( builder ) =>
    {
      builder.OpenElement<WitchChoicesDesc>( 0 );
      builder.AddAttribute<WitchChoicesDesc>( 1, RealTime.Now, ( panel ) => panel.Target = target );
      builder.CloseElement();
    } );
  }


  // OnPostDay
  // OnPostNight

  // OnPreDay
  // OnPreNight

  // OnBeforePlayerDead
  // OnPlayerKill

  // OnNightTurn


  // OnTask

}

// CheckWin
