# Third-party modification record / 第三方修改记录

Product: Plain Craft Launcher (PCL) 老蒋定制版. Display name: PCL老蒋定制版. Display version: P1. Internal version: 1.0.0-p1.

Original author / 原作者：龙腾猫跃。赞助链接：https://ifdian.net/a/LTCat 。

Community base / 社区基础：PCL Community, PCL CE v2.15.1, commit `1a7653127d578efdb2a9388999a3fdc67d9e49d5`.

This is an independent third-party modification, not an official PCL or PCL CE release. The unchanged reference checkout is kept separately under `work/upstream/pcl-ce`.

## Licensing boundaries

- `Plain Craft Launcher 2/`: retain and read `Plain Craft Launcher 2/LICENCE`. Its limited distribution license and separate reasonable-use guide are distinct documents. The latter explicitly states it is explanatory, not a legal agreement. This record does not expand either document's permissions.
- Other original directories: see the original root `LICENSE` and component notices. Do not apply the root Apache license to the launcher directory.
- Bundled assets and dependencies: retain their original declarations in `metadata.json` and component notices. Downloaded games, Java runtimes, mods and shader packs are not bundled with the launcher source or binary package.
- Source is available locally. Public publication and distribution clearance have not been completed; this local preview is not a fully cleared public release.

## Changes

- P1 branding, a Minecraft voxel landscape home page, and localized game/mod/shader shortcuts. The launcher retains the original PCL icon; the installer adds a small install badge to the same original artwork. Icon origin, pinned commit and hashes are recorded in Images/Branding/icon-sources.json. The landscape is AI-generated artwork, not a gameplay verification screenshot; see the workspace visual asset record.
- Native EXE entry and graphical installer/uninstaller are implemented in src/Packaging. No CMD or PowerShell is used to start the installed launcher. Installer and core retain separate runtime responsibilities, and upgrades/uninstall preserve user data.

- Independent name, startup/about notice and User-Agent. Original author attribution and sponsorship link remain first in the about list.
- Portable launcher data, configuration, caches, temporary files and logs. Stop automatically discovering the official launcher's game directory. No migration of upstream accounts or configurations.
- Microsoft and CurseForge credentials come only from dedicated project environment variables. The workspace startup script optionally reads ignored project-local JSON.
- Replace the geographic installation gate with the distribution's explicit download capability configuration. Language and Windows region do not disable installation.
- Reuse the original game/component installers and resource managers. Map exact public artifact hosts to BMCLAPI and identify each provider separately.
- Stop implicitly proxying Modrinth/CurseForge through an unrelated API mirror. Fix requests that previously queued no original URL when no mirror mapping existed. Unconfigured CurseForge does not interrupt Modrinth search.
- HTTP 401/403 stops file-download retries. Preserve completed files until the replacement passes verification. Re-sign CurseForge download redirects for each destination without forwarding the key to unrelated hosts.
- Remove inherited binary update endpoints. No independent update service is configured yet.
- Chinese/English distribution texts and missing en-GB keys. Other inherited languages have explicit English fallback for missing keys; their translation quality is not certified.
- Retain the blue theme family, purchase reminders and author sponsorship reminders. Explicit demo verification uses --demo and does not claim paid entitlement.
- Lazy WPF font URI creation allows culture classification in test hosts.
- Add source-routing tests, localhost rejection/hash-failure checks, multilingual WPF rendering checks, and opt-in acceptance using the actual installer and launcher.

See the workspace README and reports/acceptance-*.json for build and verification details. Inherited build warnings remain; compilation does not verify every inherited feature.
