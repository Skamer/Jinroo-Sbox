namespace Jinroo;

public class VillagersRole : ARole
{
  public VillagersRole()
  {
    Team = RoleTeam.VILLAGE;
    Type = RoleType.VILLAGER;
  }

  public const string Key = "villagers";
  public override string GetKey() => Key;

  public override string GetName() => "Villagers";

  public override int GetOrder() => (int)RoleOrder.VILLAGERS;

  public override bool IsUnique() => false;

  public override string GetIcon() => "/textures/roles/card_role_villagers.svg";

  public override string GetAbilityText()
  {
    return "The villagers has no special ability, excepted of trying persuade the others to vote against someone.";
  }
}