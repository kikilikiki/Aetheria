using Aetheria.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Database.Context;

/// <summary>
/// Point d'accès EF Core unique à la base de données Aetheria (PostgreSQL). Regroupe les
/// tables décrites dans <c>Docs/GameDesign.md</c> : Users, Characters, Monsters, Inventory,
/// Achievements, Statistics, Guilds, Kingdoms, Items, Quests, Collections, Leaderboard.
/// </summary>
public sealed class AetheriaDbContext(DbContextOptions<AetheriaDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();
    public DbSet<MonsterEntity> Monsters => Set<MonsterEntity>();
    public DbSet<InventoryItemEntity> InventoryItems => Set<InventoryItemEntity>();
    public DbSet<AchievementEntity> Achievements => Set<AchievementEntity>();
    public DbSet<StatisticsEntity> Statistics => Set<StatisticsEntity>();
    public DbSet<GuildEntity> Guilds => Set<GuildEntity>();
    public DbSet<KingdomEntity> Kingdoms => Set<KingdomEntity>();
    public DbSet<ItemEntity> Items => Set<ItemEntity>();
    public DbSet<QuestEntity> Quests => Set<QuestEntity>();
    public DbSet<CharacterQuestProgressEntity> CharacterQuestProgress => Set<CharacterQuestProgressEntity>();
    public DbSet<CollectionEntity> Collections => Set<CollectionEntity>();
    public DbSet<LeaderboardEntity> Leaderboard => Set<LeaderboardEntity>();
    public DbSet<MonsterSpeciesEntity> MonsterSpecies => Set<MonsterSpeciesEntity>();
    public DbSet<DungeonEntity> Dungeons => Set<DungeonEntity>();
    public DbSet<CharacterProfessionEntity> CharacterProfessions => Set<CharacterProfessionEntity>();
    public DbSet<RecipeEntity> Recipes => Set<RecipeEntity>();
    public DbSet<RecipeIngredientEntity> RecipeIngredients => Set<RecipeIngredientEntity>();
    public DbSet<GuildMemberEntity> GuildMembers => Set<GuildMemberEntity>();
    public DbSet<TerritoryEntity> Territories => Set<TerritoryEntity>();
    public DbSet<SeasonEntity> Seasons => Set<SeasonEntity>();
    public DbSet<PartyEntity> Parties => Set<PartyEntity>();
    public DbSet<PartyMemberEntity> PartyMembers => Set<PartyMemberEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(user =>
        {
            user.HasIndex(u => u.Username).IsUnique();
            user.HasIndex(u => u.Email).IsUnique();
            user.Property(u => u.Rank).HasConversion<string>();
        });

        modelBuilder.Entity<CharacterEntity>(character =>
        {
            character.HasIndex(c => c.Name).IsUnique();

            character.HasOne(c => c.User)
                .WithMany(u => u.Characters)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            character.HasOne(c => c.Statistics)
                .WithOne(s => s.Character)
                .HasForeignKey<StatisticsEntity>(s => s.CharacterId);

            character.Property(c => c.Class).HasConversion<string>();
            character.Property(c => c.Kingdom).HasConversion<string>();
        });

        modelBuilder.Entity<MonsterEntity>(monster =>
        {
            monster.HasOne(m => m.OwnerCharacter)
                .WithMany(c => c.Monsters)
                .HasForeignKey(m => m.OwnerCharacterId)
                .OnDelete(DeleteBehavior.Cascade);

            monster.Property(m => m.Variant).HasConversion<string>();
        });

        modelBuilder.Entity<InventoryItemEntity>(inventory =>
        {
            inventory.ToTable("Inventory");

            inventory.HasOne(i => i.Character)
                .WithMany(c => c.InventoryItems)
                .HasForeignKey(i => i.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);

            inventory.HasOne(i => i.Item)
                .WithMany()
                .HasForeignKey(i => i.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ItemEntity>(item =>
        {
            item.Property(i => i.ItemType).HasConversion<string>();
            item.Property(i => i.Rarity).HasConversion<string>();
        });

        modelBuilder.Entity<StatisticsEntity>(stats =>
        {
            stats.OwnsOne(s => s.Combat);
            stats.OwnsOne(s => s.Exploration);
            stats.OwnsOne(s => s.Monsters);
            stats.OwnsOne(s => s.Economy);
            stats.OwnsOne(s => s.Pvp);
            stats.OwnsOne(s => s.Social);
        });

        modelBuilder.Entity<GuildEntity>(guild =>
        {
            guild.HasIndex(g => g.Name).IsUnique();
            guild.HasOne(g => g.LeaderCharacter)
                .WithMany()
                .HasForeignKey(g => g.LeaderCharacterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KingdomEntity>()
            .Property(k => k.Type)
            .HasConversion<string>();

        modelBuilder.Entity<AchievementEntity>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CollectionEntity>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CharacterQuestProgressEntity>(progress =>
        {
            progress.HasOne(p => p.Character)
                .WithMany()
                .HasForeignKey(p => p.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);

            progress.HasOne(p => p.Quest)
                .WithMany()
                .HasForeignKey(p => p.QuestId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LeaderboardEntity>(leaderboard =>
        {
            leaderboard.HasOne(l => l.Character)
                .WithMany()
                .HasForeignKey(l => l.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);

            leaderboard.HasIndex(l => new { l.Category, l.Score });
            leaderboard.Property(l => l.Category).HasConversion<string>();
        });

        modelBuilder.Entity<MonsterSpeciesEntity>(species =>
        {
            species.Property(s => s.Element).HasConversion<string>();
            species.Property(s => s.BaseRarity).HasConversion<string>();
        });

        modelBuilder.Entity<DungeonEntity>()
            .HasOne(d => d.Kingdom)
            .WithMany()
            .HasForeignKey(d => d.KingdomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CharacterProfessionEntity>(profession =>
        {
            profession.HasIndex(p => new { p.CharacterId, p.Profession }).IsUnique();
            profession.HasOne(p => p.Character)
                .WithMany()
                .HasForeignKey(p => p.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);
            profession.Property(p => p.Profession).HasConversion<string>();
        });

        modelBuilder.Entity<RecipeEntity>(recipe =>
        {
            recipe.Property(r => r.Profession).HasConversion<string>();
            recipe.HasOne(r => r.ResultItem)
                .WithMany()
                .HasForeignKey(r => r.ResultItemId)
                .OnDelete(DeleteBehavior.Restrict);
            recipe.HasMany(r => r.Ingredients)
                .WithOne(i => i.Recipe)
                .HasForeignKey(i => i.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeIngredientEntity>()
            .HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GuildMemberEntity>(member =>
        {
            member.HasIndex(m => m.CharacterId).IsUnique();

            member.HasOne(m => m.Guild)
                .WithMany()
                .HasForeignKey(m => m.GuildId)
                .OnDelete(DeleteBehavior.Cascade);

            member.HasOne(m => m.Character)
                .WithMany()
                .HasForeignKey(m => m.CharacterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PartyEntity>(party =>
        {
            party.HasOne(p => p.LeaderCharacter)
                .WithMany()
                .HasForeignKey(p => p.LeaderCharacterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PartyMemberEntity>(member =>
        {
            member.HasIndex(m => m.CharacterId).IsUnique();

            member.HasOne(m => m.Party)
                .WithMany()
                .HasForeignKey(m => m.PartyId)
                .OnDelete(DeleteBehavior.Cascade);

            member.HasOne(m => m.Character)
                .WithMany()
                .HasForeignKey(m => m.CharacterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TerritoryEntity>(territory =>
        {
            territory.Property(t => t.TerritoryType).HasConversion<string>();
            territory.HasOne(t => t.ControllingKingdom)
                .WithMany()
                .HasForeignKey(t => t.ControllingKingdomId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
