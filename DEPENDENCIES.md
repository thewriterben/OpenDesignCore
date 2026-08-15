# Dependencies

Every dependency is a liability someone else controls. Each one gets a row before it gets a commit.

| Package | Version | License | Why nothing already here will do |
|---|---|---|---|
| ⟨…⟩ | ⟨…⟩ | ⟨…⟩ | ⟨…⟩ |

## License compatibility

Project license: MIT.

MIT is permissive, so most things combine cleanly — but the constraint runs the other way too. Check before adding, especially for geometry kernels, meshers, and solvers: several widely used ones are copyleft or carry linking conditions, and pulling one in can restrict how downstream users ship work built on OpenDesignCore. Verify the license text of the specific version rather than trusting a summary, and if the answer has commercial consequences, ask a lawyer.

## Format policy

Prefer interchange formats that outlive the project: STEP, 3MF, glTF, plain text. No proprietary-only format in the core; adapters live at the edges.
