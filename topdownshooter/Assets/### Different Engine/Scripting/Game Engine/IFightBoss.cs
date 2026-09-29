// a final boss with a fight of its own (Yama, Meng Po): the spawner makes it and hands it the
// fight, and it comes in its own way, runs its phases, and calls FinalBossDown when it falls
public interface IFightBoss
{
    void Begin(SpawnDirector spawner, EnemyArchetype archetype);
}
