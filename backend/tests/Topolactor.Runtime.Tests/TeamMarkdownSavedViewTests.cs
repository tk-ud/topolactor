using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

/// <summary>
/// Tests for Team Markdown Dashboard Saved View.
/// Covers:
///   - CompletedPresetSeedValidator blocking gate (no DB required)
///   - Template and saved view CRUD round-trip against live PostgreSQL
///   - Seed validation failure blocks create/refresh/clone operations
///   - Refresh uses completed_preset_seed_json, not Markdown body parsing
///   - User adjustment patch is preserved during refresh
///
/// TOPOLACTOR_TEST_DB_CONNECTION unset: live-DB tests return early (intentional conditional skip,
/// not a vacuous pass — tests make no assertions when skipped).
/// Set TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require a live database in CI (throws if unset).
///
/// SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml
/// </summary>
[Trait("Category", "TeamMarkdownSavedView")]
public class TeamMarkdownSavedViewTests
{
    private static string? GetConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return null;
        }
        return cs;
    }

    // ─── CompletedPresetSeedValidator unit tests (no DB) ─────────────────────

    [Fact]
    public void SeedValidator_RejectsNonObject()
    {
        var json = JsonSerializer.SerializeToElement("not_an_object");
        var error = CompletedPresetSeedValidator.Validate(json);
        Assert.NotNull(error);
        Assert.Equal("COMPLETED_PRESET_SEED_INVALID", error!.Code);
    }

    [Fact]
    public void SeedValidator_RejectsMissingRequiredField()
    {
        var seed = JsonSerializer.SerializeToElement(new
        {
            seed_version = "1",
            template_ref = new { },
            source_ref = new { },
            binding_ref = new { },
            render_ref = new { rendered_markdown_hash = "abc123" },
            adjustment_ref = new { }
            // dashboard_ref and lineage_ref are MISSING
        });
        var error = CompletedPresetSeedValidator.Validate(seed);
        Assert.NotNull(error);
        Assert.Equal("COMPLETED_PRESET_SEED_MISSING", error!.Code);
    }

    [Fact]
    public void SeedValidator_RejectsMissingRenderHash()
    {
        var seed = BuildValidSeed("");
        var error = CompletedPresetSeedValidator.Validate(seed);
        Assert.NotNull(error);
        Assert.Equal("COMPLETED_PRESET_SEED_RENDER_HASH_MISMATCH", error!.Code);
    }


    [Fact]
    public void SeedValidator_RejectsUnresolvedRequiredPlaceholderKeys()
    {
        var seed = JsonSerializer.SerializeToElement(new
        {
            seed_version = "md_translation_authoring_seed_registration.v1",
            template_ref = new { template_id = Guid.NewGuid().ToString(), template_key = "daily_note" },
            source_ref = new { source_table_ref = "topology.source", source_record_ref = "record-1" },
            binding_ref = new
            {
                binding_json = new { owner = new { source_kind = "physical_table_column", field_ref = "owner" } },
                placeholder_to_field_map = new { owner = "owner" },
                required_placeholder_keys = new[] { "owner" },
                optional_placeholder_keys = Array.Empty<string>(),
                unresolved_required_placeholder_keys = new[] { "owner" }
            },
            render_ref = new
            {
                rendered_markdown_hash = "abc123",
                rendered_at = DateTime.UtcNow.ToString("o"),
                renderer_version = "test",
                unresolved_placeholder_keys = Array.Empty<string>()
            },
            adjustment_ref = new { adjustment_mode = "none", user_adjustment_patch_json = new { } },
            dashboard_ref = new { title = "Daily", excerpt = "Daily", tags = Array.Empty<string>(), card_metadata_json = new { }, search_index_basis_json = new { } },
            lineage_ref = new { created_from = "md_translation_authoring_seed_registration", parent_saved_view_id = (string?)null }
        });

        var error = CompletedPresetSeedValidator.Validate(seed);
        Assert.NotNull(error);
        Assert.Equal("REQUIRED_PLACEHOLDER_UNBOUND", error!.Code);
    }

    [Fact]
    public void SeedValidator_RejectsUnresolvedRenderPlaceholderKeys()
    {
        var seed = JsonSerializer.SerializeToElement(new
        {
            seed_version = "md_translation_authoring_seed_registration.v1",
            template_ref = new { template_id = Guid.NewGuid().ToString(), template_key = "daily_note" },
            source_ref = new { source_table_ref = "topology.source", source_record_ref = "record-1" },
            binding_ref = new
            {
                binding_json = new { owner = new { source_kind = "physical_table_column", field_ref = "owner" } },
                placeholder_to_field_map = new { owner = "owner" },
                required_placeholder_keys = new[] { "owner" },
                optional_placeholder_keys = Array.Empty<string>(),
                unresolved_required_placeholder_keys = Array.Empty<string>()
            },
            render_ref = new
            {
                rendered_markdown_hash = "abc123",
                rendered_at = DateTime.UtcNow.ToString("o"),
                renderer_version = "test",
                unresolved_placeholder_keys = new[] { "owner" }
            },
            adjustment_ref = new { adjustment_mode = "none", user_adjustment_patch_json = new { } },
            dashboard_ref = new { title = "Daily", excerpt = "Daily", tags = Array.Empty<string>(), card_metadata_json = new { }, search_index_basis_json = new { } },
            lineage_ref = new { created_from = "md_translation_authoring_seed_registration", parent_saved_view_id = (string?)null }
        });

        var error = CompletedPresetSeedValidator.Validate(seed);
        Assert.NotNull(error);
        Assert.Equal("REQUIRED_PLACEHOLDER_UNBOUND", error!.Code);
    }

    [Fact]
    public void SeedValidator_AcceptsCompleteValidSeed()
    {
        var seed = BuildValidSeed();
        var error = CompletedPresetSeedValidator.Validate(seed);
        Assert.Null(error);
    }

    [Fact]
    public void SeedValidator_RejectsNestedMissingBindingRequiredKeys()
    {
        var seed = JsonSerializer.SerializeToElement(new
        {
            seed_version = "1",
            template_ref = new { template_id = Guid.NewGuid().ToString(), template_key = "test" },
            source_ref = new { source_table_ref = "topology.source", source_record_ref = "record-1" },
            binding_ref = new { binding_json = new { }, placeholder_to_field_map = new { } },
            render_ref = new { rendered_markdown_hash = "abc", rendered_at = DateTime.UtcNow.ToString("o"), renderer_version = "test", unresolved_placeholder_keys = Array.Empty<string>() },
            adjustment_ref = new { adjustment_mode = "none", user_adjustment_patch_json = new { } },
            dashboard_ref = new { title = "Daily", excerpt = "Daily", tags = Array.Empty<string>(), card_metadata_json = new { }, search_index_basis_json = new { } },
            lineage_ref = new { created_from = "test", parent_saved_view_id = (string?)null }
        });
        var error = CompletedPresetSeedValidator.Validate(seed);
        Assert.NotNull(error);
        Assert.Equal("COMPLETED_PRESET_SEED_MISSING", error!.Code);
    }

    [Fact]
    public void MarkdownBindingRenderer_ResolvesExplicitBindingsAndOptionalEmptyWithoutMarkdownInference()
    {
        var seed = BuildValidSeed();
        var sourceRecord = JsonSerializer.SerializeToElement(new { title = "Incident A", json = new { summary = "Resolved" } });
        var result = MarkdownBindingRenderer.Render("# {{title}}\n{{body}}\n{{note}}", seed, sourceRecord);
        Assert.True(result.Ok, result.Message);
        Assert.Equal("# Incident A\nResolved\n", result.RenderedMarkdown);
        Assert.Contains("note", result.ExplicitOptionalEmptyPlaceholderKeys);
        Assert.StartsWith("sha256:", result.RenderedMarkdownHash);
    }

    [Fact]
    public void MarkdownBindingRenderer_BlocksUnresolvedRequiredPlaceholder()
    {
        var seed = BuildValidSeed();
        var sourceRecord = JsonSerializer.SerializeToElement(new { title = "Incident A", json = new { } });
        var result = MarkdownBindingRenderer.Render("# {{title}}\n{{body}}", seed, sourceRecord);
        Assert.False(result.Ok);
        Assert.Equal("JSONB_PATH_INVALID", result.ErrorCode);
    }

    [Fact]
    public void PresetSeedRegistrationBootstrap_UsesExistingTeamMarkdownTemplateRegistry()
    {
        var migrationPath = ResolveRepoPath("db/migrations/team_markdown_registry_tables.sql");
        var migration = File.ReadAllText(migrationPath);
        Assert.Contains("md_viewer.team_markdown.saved_view.completed_seed.v1", migration);
        Assert.Contains("topology.team_markdown_template_registry", migration);
        Assert.Contains("UIBuilder.preset_ecosystem", migration);
        Assert.DoesNotContain("markdown_preset_registry", migration);
    }

    [Fact]
    public void MarkdownBindingRenderer_RequiresSourceRecordObject()
    {
        var result = MarkdownBindingRenderer.Render("# {{title}}", BuildValidSeed(), null);
        Assert.False(result.Ok);
        Assert.Equal("SOURCE_RECORD_NOT_FOUND", result.ErrorCode);
    }

    // ─── AdminRuntime dispatch contract tests (no DB) ───────────────────────

    [Fact]
    public async Task AdminRuntimeCreate_BlocksRenderedMarkdownHashMismatch()
    {
        var repo = new StubTeamMarkdownRepo();
        var runtime = CreateRuntime(repo);
        var payload = BuildCreatePayload("# actual", BuildValidSeed("sha256:not-the-actual-hash"));

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "team_markdown", "saved_view:create", null, "admin", payload, null), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("COMPLETED_PRESET_SEED_RENDER_HASH_MISMATCH", error!.Code);
        Assert.Equal(0, repo.CreateCount);
    }

    [Fact]
    public async Task AdminRuntimeUpdate_BlocksSeedlessRenderedMarkdownUpdate()
    {
        var existing = BuildSavedViewDetail(renderedMarkdown: "# existing", seed: BuildValidSeedForMarkdown("# existing"));
        var repo = new StubTeamMarkdownRepo(existing);
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { renderedMarkdown = "# changed" });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "team_markdown", "saved_view:update", null, "admin", payload, null, IdOrHubId: Guid.Parse(existing.SavedViewId)), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("COMPLETED_PRESET_SEED_MISSING", error!.Code);
        Assert.Equal("# existing", repo.Detail!.RenderedMarkdown);
    }

    [Fact]
    public async Task AdminRuntimeUpdate_BlocksRenderedMarkdownHashMismatch()
    {
        var existing = BuildSavedViewDetail(renderedMarkdown: "# existing", seed: BuildValidSeedForMarkdown("# existing"));
        var repo = new StubTeamMarkdownRepo(existing);
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            renderedMarkdown = "# changed",
            completedPresetSeedJson = BuildValidSeed("sha256:mismatch")
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "team_markdown", "saved_view:update", null, "admin", payload, null, IdOrHubId: Guid.Parse(existing.SavedViewId)), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("COMPLETED_PRESET_SEED_RENDER_HASH_MISMATCH", error!.Code);
        Assert.Equal("# existing", repo.Detail!.RenderedMarkdown);
    }

    [Fact]
    public async Task AdminRuntimeRefreshExplicitCompatibility_BlocksRenderedMarkdownHashMismatch()
    {
        var existing = BuildSavedViewDetail(renderedMarkdown: "# existing", seed: BuildValidSeedForMarkdown("# existing"));
        var repo = new StubTeamMarkdownRepo(existing);
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            refreshedRenderedMarkdown = "# refreshed",
            updatedCompletedPresetSeedJson = BuildValidSeed("sha256:mismatch"),
            searchIndexText = "refreshed"
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "team_markdown", "saved_view:refresh", null, "admin", payload, null, IdOrHubId: Guid.Parse(existing.SavedViewId)), default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("COMPLETED_PRESET_SEED_RENDER_HASH_MISMATCH", error!.Code);
        Assert.Equal("# existing", repo.Detail!.RenderedMarkdown);
    }

    [Fact]
    public async Task AdminRuntimeRebind_UpdatesDbBindingJsonAndSeedBindingTogether()
    {
        var existing = BuildSavedViewDetail(renderedMarkdown: "# Old\nBody\n", seed: BuildValidSeedForMarkdown("# Old\nBody\n"));
        var repo = new StubTeamMarkdownRepo(existing);
        var runtime = CreateRuntime(repo);
        var newBinding = JsonSerializer.SerializeToElement(new
        {
            title = new { source_kind = "physical_table_column", field_ref = "newTitle" },
            body = new { source_kind = "physical_table_jsonb_path", field_ref = "json.newSummary" },
            note = new { source_kind = "explicit_optional_empty", field_ref = "" }
        });
        var proposedSeed = BuildValidSeedForMarkdown("# placeholder", bindingJson: newBinding);
        var payload = JsonSerializer.SerializeToElement(new
        {
            bindingJson = newBinding,
            completedPresetSeedJson = proposedSeed,
            templateMarkdown = "# {{title}}\n{{body}}\n{{note}}",
            sourceRecordJson = new { newTitle = "New", json = new { newSummary = "Bound" } },
            searchIndexText = "new bound"
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "team_markdown", "saved_view:rebind", null, "admin", payload, null, IdOrHubId: Guid.Parse(existing.SavedViewId)), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.Equal("# New\nBound\n", repo.Detail!.RenderedMarkdown);
        Assert.True(MarkdownBindingRenderer.JsonSemanticallyEquals(newBinding, repo.Detail.BindingJson));
        Assert.True(MarkdownBindingRenderer.JsonSemanticallyEquals(
            newBinding,
            repo.Detail.CompletedPresetSeedJson.GetProperty("binding_ref").GetProperty("binding_json")));
    }

    [Fact]
    public async Task AdminRuntimeSeedInvalid_BlocksRefreshCloneAndRebind()
    {
        var invalidSeed = JsonSerializer.SerializeToElement(new { seed_version = "1" });
        var existing = BuildSavedViewDetail(renderedMarkdown: "# existing", seed: invalidSeed);
        var repo = new StubTeamMarkdownRepo(existing);
        var runtime = CreateRuntime(repo);

        foreach (var action in new[] { "saved_view:refresh", "saved_view:clone", "saved_view:rebind" })
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                templateMarkdown = "# {{title}}",
                sourceRecordJson = new { title = "x" },
                targetSourceTableRef = "topology.other",
                targetSourceRecordRef = "record-2",
                bindingJson = new { },
                completedPresetSeedJson = BuildValidSeedForMarkdown("# x")
            });
            var (data, error) = await runtime.ExecuteDataAsync(
                new OperationVector("admin", "team_markdown", action, null, "admin", payload, null, IdOrHubId: Guid.Parse(existing.SavedViewId)), default);

            Assert.Null(data);
            Assert.NotNull(error);
            Assert.Equal("COMPLETED_PRESET_SEED_MISSING", error!.Code);
        }
    }

    [Fact]
    public async Task AdminRuntimeRefresh_PreservesUserAdjustmentPatch()
    {
        var patch = JsonSerializer.SerializeToElement(new { note = "keep" });
        var seed = BuildValidSeedForMarkdown("# Old\nBody\n", adjustmentPatch: patch);
        var existing = BuildSavedViewDetail(renderedMarkdown: "# Old\nBody\n", seed: seed, adjustmentPatch: patch);
        var repo = new StubTeamMarkdownRepo(existing);
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            templateMarkdown = "# {{title}}\n{{body}}\n{{note}}",
            sourceRecordJson = new { title = "New", json = new { summary = "Body" } },
            searchIndexText = "new body"
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "team_markdown", "saved_view:refresh", null, "admin", payload, null, IdOrHubId: Guid.Parse(existing.SavedViewId)), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(MarkdownBindingRenderer.JsonSemanticallyEquals(patch, repo.Detail!.UserAdjustmentPatchJson));
        Assert.True(MarkdownBindingRenderer.JsonSemanticallyEquals(
            patch,
            repo.Detail.CompletedPresetSeedJson.GetProperty("adjustment_ref").GetProperty("user_adjustment_patch_json")));
    }

    // ─── Live DB tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTemplate_InsertsRowAndReturnsTemplateId()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var repo = new NpgsqlTeamMarkdownRepository(
            NullLogger<NpgsqlTeamMarkdownRepository>.Instance, cs);

        var request = new TeamMarkdownTemplateCreateRequest(
            TemplateKey: $"test_template_{suffix}",
            TemplateLabel: "Test Template",
            TemplateMarkdown: "# {{record.summary}}\n{{record.description}}",
            PlaceholderSchemaJson: JsonSerializer.SerializeToElement(new
            {
                placeholders = new[] { "record.summary", "record.description" }
            })
        );

        var (templateId, errorCode, message) = await repo.CreateTemplateAsync(request);
        try
        {
            Assert.Null(errorCode);
            Assert.NotNull(templateId);
            Assert.True(Guid.TryParse(templateId, out _));

            var (fetched, fetchErr, _) = await repo.GetTemplateAsync(Guid.Parse(templateId!));
            Assert.Null(fetchErr);
            Assert.NotNull(fetched);
            Assert.Equal(request.TemplateKey, fetched!.TemplateKey);
            Assert.Equal(request.TemplateMarkdown, fetched.TemplateMarkdown);
        }
        finally
        {
            if (templateId is not null)
                await CleanupTemplateAsync(cs, Guid.Parse(templateId));
        }
    }

    [Fact]
    public async Task CreateSavedView_RequiresCompletedPresetSeedJson()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var templateRepo = new NpgsqlTeamMarkdownRepository(
            NullLogger<NpgsqlTeamMarkdownRepository>.Instance, cs);

        var (templateId, _, _) = await templateRepo.CreateTemplateAsync(new TeamMarkdownTemplateCreateRequest(
            $"test_tpl_{suffix}", "Test Template", "# {{record.summary}}", JsonSerializer.SerializeToElement(new { })));
        Assert.NotNull(templateId);

        try
        {
            var request = new TeamMarkdownSavedViewCreateRequest(
                TemplateId: templateId!,
                Title: "Test Saved View",
                SourceTableRef: "topology.physical_tables",
                SourceRecordRef: "test_record_ref",
                BindingJson: JsonSerializer.SerializeToElement(new { }),
                CompletedPresetSeedJson: BuildValidSeed(),
                RenderedMarkdown: "# Test Summary",
                UserAdjustmentPatchJson: JsonSerializer.SerializeToElement(new { }),
                SearchIndexText: "Test Summary",
                CardMetadataJson: JsonSerializer.SerializeToElement(new { excerpt = "Test" })
            );

            var (savedViewId, errorCode, message) = await templateRepo.CreateSavedViewAsync(request);
            try
            {
                Assert.Null(errorCode);
                Assert.NotNull(savedViewId);

                var (fetched, fetchViewErr, _) = await templateRepo.GetSavedViewAsync(Guid.Parse(savedViewId!));
                Assert.Null(fetchViewErr);
                Assert.NotNull(fetched);
                Assert.Equal("Test Saved View", fetched!.Title);
                Assert.Equal("active", fetched.Status);

                var seedError = CompletedPresetSeedValidator.Validate(fetched.CompletedPresetSeedJson);
                Assert.Null(seedError);
            }
            finally
            {
                if (savedViewId is not null)
                    await CleanupSavedViewAsync(cs, Guid.Parse(savedViewId));
            }
        }
        finally
        {
            await CleanupTemplateAsync(cs, Guid.Parse(templateId!));
        }
    }

    [Fact]
    public async Task SearchSavedViews_ReturnsSavedViewCards()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var renderedTerm = "rendered_only_" + suffix;
        var tagTerm = "tag_only_" + suffix;
        var repo = new NpgsqlTeamMarkdownRepository(
            NullLogger<NpgsqlTeamMarkdownRepository>.Instance, cs);

        var (templateId, _, _) = await repo.CreateTemplateAsync(new TeamMarkdownTemplateCreateRequest(
            $"test_tpl_search_{suffix}", "Search Test Template", "# {{record.summary}}", JsonSerializer.SerializeToElement(new { })));
        Assert.NotNull(templateId);

        var (savedViewId, _, _) = await repo.CreateSavedViewAsync(new TeamMarkdownSavedViewCreateRequest(
            TemplateId: templateId!,
            Title: "Searchable View",
            SourceTableRef: "topology.physical_tables",
            SourceRecordRef: "record_search",
            BindingJson: JsonSerializer.SerializeToElement(new { }),
            CompletedPresetSeedJson: BuildValidSeed(),
            RenderedMarkdown: $"# Summary {renderedTerm}",
            UserAdjustmentPatchJson: JsonSerializer.SerializeToElement(new { }),
            SearchIndexText: "summary description",
            CardMetadataJson: JsonSerializer.SerializeToElement(new { excerpt = "Test excerpt", tags = new[] { tagTerm } })
        ));
        Assert.NotNull(savedViewId);

        try
        {
            var (renderedResults, searchErr2, _) = await repo.SearchSavedViewsAsync(renderedTerm);
            Assert.Null(searchErr2);
            Assert.Contains(renderedResults, c => c.SavedViewId == savedViewId);

            var (tagResults, tagSearchErr, _) = await repo.SearchSavedViewsAsync(tagTerm);
            Assert.Null(tagSearchErr);
            Assert.Contains(tagResults, c => c.SavedViewId == savedViewId);

            var (statusResults, statusSearchErr, _) = await repo.SearchSavedViewsAsync(null, "active");
            Assert.Null(statusSearchErr);
            Assert.Contains(statusResults, c => c.SavedViewId == savedViewId && c.Status == "active");
        }
        finally
        {
            await CleanupSavedViewAsync(cs, Guid.Parse(savedViewId!));
            await CleanupTemplateAsync(cs, Guid.Parse(templateId!));
        }
    }

    [Fact]
    public async Task RefreshSavedView_DoesNotUseSavedMarkdownBodyForRefresh()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var repo = new NpgsqlTeamMarkdownRepository(
            NullLogger<NpgsqlTeamMarkdownRepository>.Instance, cs);

        var (templateId, _, _) = await repo.CreateTemplateAsync(new TeamMarkdownTemplateCreateRequest(
            $"test_tpl_refresh_{suffix}", "Refresh Test Template", "# {{record.summary}}", JsonSerializer.SerializeToElement(new { })));

        var originalMarkdown = $"# Original Summary {suffix}";
        var (savedViewId, _, _) = await repo.CreateSavedViewAsync(new TeamMarkdownSavedViewCreateRequest(
            TemplateId: templateId!,
            Title: "Refresh Test View",
            SourceTableRef: "topology.physical_tables",
            SourceRecordRef: "record_search",
            BindingJson: JsonSerializer.SerializeToElement(new { binding = "record.summary" }),
            CompletedPresetSeedJson: BuildValidSeed(),
            RenderedMarkdown: originalMarkdown,
            UserAdjustmentPatchJson: JsonSerializer.SerializeToElement(new { note = "original note" }),
            SearchIndexText: "original",
            CardMetadataJson: JsonSerializer.SerializeToElement(new { excerpt = "original" })
        ));

        try
        {
            var refreshedMarkdown = $"# Refreshed Summary {suffix}";
            var updatedSeed = BuildValidSeed("refreshed_hash_" + suffix);

            var (updated, errorCode, message) = await repo.UpdateSavedViewAsync(
                Guid.Parse(savedViewId!),
                title: null,
                renderedMarkdown: refreshedMarkdown,
                userAdjustmentPatchJson: null,
                completedPresetSeedJson: updatedSeed,
                searchIndexText: "refreshed",
                cardMetadataJson: null,
                bindingJson: null
            );
            Assert.Null(errorCode);
            Assert.True(updated);

            var (fetched, fetchErr2, _) = await repo.GetSavedViewAsync(Guid.Parse(savedViewId!));
            Assert.Null(fetchErr2);
            Assert.NotNull(fetched);
            Assert.Equal(refreshedMarkdown, fetched!.RenderedMarkdown);

            var seedError = CompletedPresetSeedValidator.Validate(fetched.CompletedPresetSeedJson);
            Assert.Null(seedError);

            Assert.NotEqual(originalMarkdown, fetched.RenderedMarkdown);
        }
        finally
        {
            await CleanupSavedViewAsync(cs, Guid.Parse(savedViewId!));
            await CleanupTemplateAsync(cs, Guid.Parse(templateId!));
        }
    }

    [Fact]
    public async Task ArchiveSavedView_ChangesStatusToArchived()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var repo = new NpgsqlTeamMarkdownRepository(
            NullLogger<NpgsqlTeamMarkdownRepository>.Instance, cs);

        var (templateId, _, _) = await repo.CreateTemplateAsync(new TeamMarkdownTemplateCreateRequest(
            $"test_tpl_archive_{suffix}", "Archive Test", "# {{record.summary}}", JsonSerializer.SerializeToElement(new { })));

        var (savedViewId, _, _) = await repo.CreateSavedViewAsync(new TeamMarkdownSavedViewCreateRequest(
            TemplateId: templateId!,
            Title: "Archive Test View",
            SourceTableRef: "topology.physical_tables",
            SourceRecordRef: "record_search",
            BindingJson: JsonSerializer.SerializeToElement(new { }),
            CompletedPresetSeedJson: BuildValidSeed(),
            RenderedMarkdown: "# Archive Test",
            UserAdjustmentPatchJson: JsonSerializer.SerializeToElement(new { }),
            SearchIndexText: "archive test",
            CardMetadataJson: JsonSerializer.SerializeToElement(new { excerpt = "archive" })
        ));

        try
        {
            var (updated, errorCode) = await repo.ArchiveSavedViewAsync(Guid.Parse(savedViewId!));
            Assert.Null(errorCode);
            Assert.True(updated);

            var (fetched, fetchErr3, _) = await repo.GetSavedViewAsync(Guid.Parse(savedViewId!));
            Assert.Null(fetchErr3);
            Assert.NotNull(fetched);
            Assert.Equal("archived", fetched!.Status);

            var (activeResults, searchErr, _) = await repo.SearchSavedViewsAsync(null, "active");
            Assert.Null(searchErr);
            Assert.DoesNotContain(activeResults, c => c.SavedViewId == savedViewId);
        }
        finally
        {
            await CleanupSavedViewAsync(cs, Guid.Parse(savedViewId!));
            await CleanupTemplateAsync(cs, Guid.Parse(templateId!));
        }
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private static AdminRuntime CreateRuntime(TeamMarkdownRepository repo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new StubUiRepoForTeamMarkdown();
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo,
            teamMarkdownRepository: repo);
    }

    private static JsonElement BuildCreatePayload(string renderedMarkdown, JsonElement seed)
        => JsonSerializer.SerializeToElement(new
        {
            templateId = Guid.NewGuid().ToString(),
            title = "Runtime Saved View",
            sourceTableRef = "topology.physical_tables",
            sourceRecordRef = "record-1",
            bindingJson = seed.ValueKind == JsonValueKind.Object &&
                          seed.TryGetProperty("binding_ref", out var bindingRef) &&
                          bindingRef.ValueKind == JsonValueKind.Object &&
                          bindingRef.TryGetProperty("binding_json", out var bindingJson)
                ? bindingJson
                : JsonSerializer.SerializeToElement(new { }),
            completedPresetSeedJson = seed,
            renderedMarkdown,
            userAdjustmentPatchJson = seed.ValueKind == JsonValueKind.Object &&
                                      seed.TryGetProperty("adjustment_ref", out var adjustmentRef) &&
                                      adjustmentRef.ValueKind == JsonValueKind.Object &&
                                      adjustmentRef.TryGetProperty("user_adjustment_patch_json", out var patch)
                ? patch
                : JsonSerializer.SerializeToElement(new { }),
            searchIndexText = renderedMarkdown,
            cardMetadataJson = new { excerpt = renderedMarkdown, tags = Array.Empty<string>() }
        });

    private static TeamMarkdownSavedViewDetail BuildSavedViewDetail(
        string renderedMarkdown,
        JsonElement seed,
        JsonElement? bindingJson = null,
        JsonElement? adjustmentPatch = null)
    {
        var binding = bindingJson ?? (seed.ValueKind == JsonValueKind.Object &&
                                      seed.TryGetProperty("binding_ref", out var bindingRef) &&
                                      bindingRef.ValueKind == JsonValueKind.Object &&
                                      bindingRef.TryGetProperty("binding_json", out var seedBinding)
            ? seedBinding
            : JsonSerializer.SerializeToElement(new { }));
        var patch = adjustmentPatch ?? (seed.ValueKind == JsonValueKind.Object &&
                                        seed.TryGetProperty("adjustment_ref", out var adjustmentRef) &&
                                        adjustmentRef.ValueKind == JsonValueKind.Object &&
                                        adjustmentRef.TryGetProperty("user_adjustment_patch_json", out var seedPatch)
            ? seedPatch
            : JsonSerializer.SerializeToElement(new { }));
        return new TeamMarkdownSavedViewDetail(
            SavedViewId: Guid.NewGuid().ToString(),
            TemplateId: Guid.NewGuid().ToString(),
            TemplateKey: "test_template",
            Title: "Runtime Saved View",
            SourceTableRef: "topology.physical_tables",
            SourceRecordRef: "record-1",
            BindingJson: binding,
            CompletedPresetSeedJson: seed,
            RenderedMarkdown: renderedMarkdown,
            UserAdjustmentPatchJson: patch,
            SearchIndexText: renderedMarkdown,
            CardMetadataJson: JsonSerializer.SerializeToElement(new { excerpt = renderedMarkdown, tags = Array.Empty<string>() }),
            Status: "active",
            CreatedAt: DateTime.UtcNow.ToString("o"),
            UpdatedAt: DateTime.UtcNow.ToString("o"));
    }

    private static JsonElement BuildValidSeedForMarkdown(
        string renderedMarkdown,
        JsonElement? bindingJson = null,
        JsonElement? adjustmentPatch = null)
        => BuildValidSeed(
            MarkdownBindingRenderer.ComputeRenderedMarkdownHash(renderedMarkdown),
            bindingJson,
            adjustmentPatch);

    private static string ResolveRepoPath(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return relativePath;
    }

    private static JsonElement BuildValidSeed(
        string renderHash = "abc123hash",
        JsonElement? bindingJsonOverride = null,
        JsonElement? adjustmentPatchOverride = null)
    {
        var bindingJson = bindingJsonOverride ?? JsonSerializer.SerializeToElement(new
        {
            title = new { source_kind = "physical_table_column", field_ref = "title" },
            body = new { source_kind = "physical_table_jsonb_path", field_ref = "json.summary" },
            note = new { source_kind = "explicit_optional_empty", field_ref = "" }
        });
        var adjustmentPatch = adjustmentPatchOverride ?? JsonSerializer.SerializeToElement(new { });
        return JsonSerializer.SerializeToElement(new
        {
            seed_version = "1",
            template_ref = new { template_id = Guid.NewGuid().ToString(), template_key = "test_template" },
            source_ref = new { source_table_ref = "topology.physical_tables", source_record_ref = "test_record" },
            binding_ref = new
            {
                binding_json = bindingJson,
                placeholder_to_field_map = new { title = "title", body = "json.summary", note = "" },
                required_placeholder_keys = new[] { "title", "body" },
                optional_placeholder_keys = new[] { "note" },
                explicit_optional_empty_placeholder_keys = new[] { "note" },
                unresolved_required_placeholder_keys = Array.Empty<string>()
            },
            render_ref = new
            {
                rendered_markdown_hash = renderHash,
                rendered_at = DateTime.UtcNow.ToString("o"),
                renderer_version = "1.0",
                unresolved_placeholder_keys = Array.Empty<string>()
            },
            adjustment_ref = new { adjustment_mode = "none", user_adjustment_patch_json = adjustmentPatch },
            dashboard_ref = new
            {
                title = "Test View",
                excerpt = "test excerpt",
                tags = new[] { "ops" },
                card_metadata_json = new { excerpt = "test excerpt", tags = new[] { "ops" } },
                search_index_basis_json = new { fields = new[] { "title", "rendered_markdown", "tags", "status" } }
            },
            lineage_ref = new { created_from = "template_record", parent_saved_view_id = (string?)null }
        });
    }

    private static async Task CleanupTemplateAsync(string cs, Guid templateId)
    {
        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM topology.team_markdown_template_registry WHERE template_id = @id";
            cmd.Parameters.AddWithValue("id", templateId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            // Best-effort cleanup: swallow intentionally so test assertion errors are not masked.
            // Orphaned rows may remain if this fails repeatedly — check DB manually.
            _ = ex;
        }
    }

    private static async Task CleanupSavedViewAsync(string cs, Guid savedViewId)
    {
        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            // Delete events first (FK child), then the saved view row.
            await using var evtCmd = conn.CreateCommand();
            evtCmd.CommandText = "DELETE FROM topology.team_markdown_saved_view_event WHERE saved_view_id = @id";
            evtCmd.Parameters.AddWithValue("id", savedViewId);
            await evtCmd.ExecuteNonQueryAsync();

            await using var svCmd = conn.CreateCommand();
            svCmd.CommandText = "DELETE FROM topology.team_markdown_saved_view WHERE saved_view_id = @id";
            svCmd.Parameters.AddWithValue("id", savedViewId);
            await svCmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            // Best-effort cleanup: swallow intentionally so test assertion errors are not masked.
            _ = ex;
        }
    }
    private class StubTeamMarkdownRepo : TeamMarkdownRepository
    {
        public TeamMarkdownSavedViewDetail? Detail { get; private set; }
        public int CreateCount { get; private set; }

        public StubTeamMarkdownRepo(TeamMarkdownSavedViewDetail? detail = null)
            : base(NullLogger<TeamMarkdownRepository>.Instance)
        {
            Detail = detail;
        }

        public override Task<(string? SavedViewId, string? ErrorCode, string? Message)> CreateSavedViewAsync(
            TeamMarkdownSavedViewCreateRequest request, CancellationToken ct = default)
        {
            CreateCount++;
            var id = Guid.NewGuid().ToString();
            Detail = new TeamMarkdownSavedViewDetail(
                id,
                request.TemplateId,
                "test_template",
                request.Title,
                request.SourceTableRef,
                request.SourceRecordRef,
                request.BindingJson,
                request.CompletedPresetSeedJson,
                request.RenderedMarkdown,
                request.UserAdjustmentPatchJson,
                request.SearchIndexText,
                request.CardMetadataJson,
                "active",
                DateTime.UtcNow.ToString("o"),
                DateTime.UtcNow.ToString("o"));
            return Task.FromResult<(string?, string?, string?)>((id, null, null));
        }

        public override Task<(string? SavedViewId, string? ErrorCode, string? Message)> CloneSavedViewAsync(
            TeamMarkdownSavedViewCreateRequest request, CancellationToken ct = default)
            => CreateSavedViewAsync(request, ct);

        public override Task<(TeamMarkdownSavedViewDetail? Detail, string? ErrorCode, string? Message)> GetSavedViewAsync(
            Guid savedViewId, CancellationToken ct = default)
            => Task.FromResult<(TeamMarkdownSavedViewDetail?, string?, string?)>((Detail, null, null));

        public override Task<(bool Updated, string? ErrorCode, string? Message)> UpdateSavedViewAsync(
            Guid savedViewId,
            string? title,
            string? renderedMarkdown,
            JsonElement? userAdjustmentPatchJson,
            JsonElement? completedPresetSeedJson,
            string? searchIndexText,
            JsonElement? cardMetadataJson,
            JsonElement? bindingJson,
            CancellationToken ct = default)
        {
            if (Detail is null) return Task.FromResult<(bool, string?, string?)>((false, "SAVED_VIEW_NOT_FOUND", "not found"));
            Detail = Detail with
            {
                Title = title ?? Detail.Title,
                RenderedMarkdown = renderedMarkdown ?? Detail.RenderedMarkdown,
                BindingJson = bindingJson ?? Detail.BindingJson,
                UserAdjustmentPatchJson = userAdjustmentPatchJson ?? Detail.UserAdjustmentPatchJson,
                CompletedPresetSeedJson = completedPresetSeedJson ?? Detail.CompletedPresetSeedJson,
                SearchIndexText = searchIndexText ?? Detail.SearchIndexText,
                CardMetadataJson = cardMetadataJson ?? Detail.CardMetadataJson,
                UpdatedAt = DateTime.UtcNow.ToString("o")
            };
            return Task.FromResult<(bool, string?, string?)>((true, null, null));
        }

        public override Task<string?> AppendEventAsync(Guid savedViewId, string eventKind, Guid? actorId,
            JsonElement eventPayloadJson, CancellationToken ct = default)
            => Task.FromResult<string?>(Guid.NewGuid().ToString());
    }

    private class StubUiRepoForTeamMarkdown()
        : UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "Host=localhost")
    {
    }
}
