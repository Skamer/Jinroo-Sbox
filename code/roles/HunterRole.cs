using System;
using System.Threading.Tasks;

namespace Jinroo;

public class HunterRole : ARole
{
  public HunterRole()
  {
    Team = RoleTeam.VILLAGE;
    Type = RoleType.VILLAGER;
  }
  public const string Key = "hunter";

  public override string GetKey() => Key;

  public override int GetOrder() => (int)RoleOrder.HUNTER;

  public override string GetName() => "Hunter";

  public override string GetIcon() => "/textures/roles/card_role_hunter.svg";

  public override string GetAbilityText()
  {
    return "Before dying, the hunter can decide who will accompany him with his rifle.";
  }

  public override async Task OnPlayerDead( TaskSource task, Player victim )
  {
    if ( Player is null || Player != victim )
      return;

    GameMode.GameState.Multicast_SetPhase( GamePhaseType.HUNTER_LAST_STAND, GetTimeout() );
    var responseData = await RequestTask( task, new(), RealTime.Now + GetTimeout() );


    try
    {
      var targetIndex = (int)responseData["target"];
      var target = GameMode.Players[targetIndex];

      target.TryKill( KillReason.HUNTER_LAST_STAND );
    }
    catch ( Exception e ) { }
  }


  public override async Task Client_OnTaskRequest( PlayerController controller, Dictionary<string, object> requestData, TaskSource task, Guid token )
  {
    var timeoutTime = RealTime.Now + GetTimeout();

    controller.SetChoicesDescription( "Before dying, you can decide to shoot on someone !" );
    controller.SetChoicesTitleIcon( GetIcon() );
    controller.SetChoicesTitle( "Interaction" );
    controller.AddChoice( "shoot", "Shoot", "", "" );
    controller.SetChoicesPanelVisible( true );

    var choice = await controller.WaitingChoices( RealTime.Now + GetTimeout() );
    controller.SetChoicesPanelVisible( false );

    if ( choice != "shoot" )
      return;

    var targets = await controller.WaitingTargets( 1, false, timeoutTime );

    if ( targets.Count != 1 )
      return;

    Dictionary<string, object> responseData = new()
    {
      { "target", targets.First().Index}
    };


    controller.Server_OnTaskResponse( responseData, token );
  }

}