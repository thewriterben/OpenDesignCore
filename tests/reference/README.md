# Reference cases

Cases with answers we didn't compute ourselves. This is the only thing standing between the project and confidently wrong numbers.

Two kinds:

1. **Analytic** — a closed-form solution exists. Sphere and torus volume at known voxel sizes, thick-walled cylinder stress, cantilever tip deflection.
2. **Published** — a benchmark from literature or a standards body, cited exactly.

## Each case records

- The source, cited precisely enough to find it again
- Inputs, with units, and the voxel size
- Expected result, with units
- Tolerance, and *why* that tolerance — discretisation error at that voxel size, floating point, or a stated bound from the source
- What it would mean if this one started failing

## Rules

- Results are only comparable at equal voxel size. Pin it in the test.
- Convergence matters as much as the value: halving the voxel size should move the answer toward the reference at the expected rate. A case that passes at one resolution and diverges as you refine is a failing case.
- A tolerance is never loosened to make a test pass. If the result moved, either the change is wrong or the tolerance was wrong for a reason you can now articulate — write which in the commit.
