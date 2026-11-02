---
severity: should-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs, xr-racing-app/Assets/Editor/**/*.cs"
enabled: true
added: 2026-11-02
---
# Fields start with an underscore

Every field declared in a class or struct is `_camelCase`, whatever its access level:
`public Camera _backgroundCamera;`, `[SerializeField] private float _jumpHeight;`,
`private static Config _config;`. Flag a new or renamed field without the `_`.

**Why:** fields are told apart from locals, parameters and properties at a glance.
**Exempt:** `const` fields (PascalCase); fields the PR doesn't add or rename.

**Renaming a serialized field** (public, or `[SerializeField]`): don't use
`[FormerlySerializedAs]`. The same PR renames the old key everywhere Unity stored it, so
no Inspector value is lost. Flag as **Must fix** any `[FormerlySerializedAs]`, and any
place the PR missed that still has the old name:
- the component's block in `.prefab`, `.unity` and `.asset` files, i.e. a `  oldName:` key
  under a `MonoBehaviour` whose `m_Script` guid is the script's `.meta` guid
- prefab and scene overrides: `propertyPath: oldName` (and `oldName.` / `oldName.Array`)
- `.anim` curves that animate the field: `attribute: oldName`
- code that uses the name as a string: `nameof`, `FindProperty("oldName")`, reflection,
  and tool scripts that read or write the YAML
Check with `grep -rn "oldName"` across the repo: after the PR, only unrelated hits remain.
**Example:** `private Camera bgCamera;` → `private Camera _bgCamera;`
