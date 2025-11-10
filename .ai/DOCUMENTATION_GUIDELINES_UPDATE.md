# OpenSpec Documentation Guidelines Update

**Date**: November 10, 2025  
**Status**: ✅ Completed  
**Impact**: Added standardized guidelines for change documentation

---

## Changes Applied

### 1. ✅ File Renaming
```bash
openspec/changes/add-screen-recording/
├── ANALYSIS_AND_FIXES.md  →  DESIGN_REVIEW.md  ✅ Renamed
```

**Rationale**: 
- More professional and standardized naming
- Follows SCREAMING_SNAKE_CASE convention for review documents
- Clearly indicates purpose (design review vs generic analysis)

---

### 2. ✅ Project Guidelines Updated

**File**: `openspec/project.md`

**Added Section**: `### OpenSpec Change Documentation`

**Key Guidelines**:

#### Required Files
- ✅ `proposal.md` - Always required
- ✅ `tasks.md` - Always required  
- ✅ `specs/[capability]/spec.md` - Always required

#### Optional Files (With Clear Criteria)
- ⚠️ `design.md` - When 3+ major design decisions
- ⚠️ `DESIGN_REVIEW.md` - When refactoring/critical issues/30%+ timeline change
- ⚠️ `ANALYSIS.md` - For exploratory/research work
- ⚠️ `DECISION_LOG.md` - For ADR (Architecture Decision Records)
- ⚠️ `docs/` - For large changes with multiple auxiliary docs

#### Decision Criteria Table

| Criteria | Threshold | Add DESIGN_REVIEW.md? |
|----------|-----------|----------------------|
| Design Changes | 3+ decisions | ✅ Yes |
| Timeline Impact | > 30% | ✅ Yes |
| Scope Impact | > 50% | ✅ Yes |
| Risk Level | HIGH/CRITICAL | ✅ Yes |
| Platform Strategies | 3+ implementations | ✅ Yes |
| Compliance | License/security | ✅ **REQUIRED** |
| Refactoring | Architecture rewrite | ✅ Yes |
| Knowledge Transfer | Complex domain | ⚠️ Consider |
| Simple Features | Straightforward | ❌ No |
| Bug Fixes | Spec restoration | ❌ No |

---

### 3. ✅ Template Provided

**Example DESIGN_REVIEW.md Structure**:
```markdown
# [Feature Name] - Design Review

**Date**: YYYY-MM-DD
**Status**: [Draft|Under Review|Approved]
**Reviewers**: [Names]

## Executive Summary
## Issues Found and Fixes Applied
## Updated Metrics
## Validation Results
## Recommendations
## Conclusion
```

---

### 4. ✅ Integration with OpenSpec Workflow

**Stage 1 (Creating Changes)**:
- Create standard files
- Add `DESIGN_REVIEW.md` if criteria met
- Validate with `openspec validate --strict`

**Stage 2 (Implementing)**:
- Use `DESIGN_REVIEW.md` as implementation reference
- Update if new issues discovered

**Stage 3 (Archiving)**:
- Archive all documents including reviews
- Preserve as historical record

---

## Validation

```bash
$ openspec validate add-screen-recording --strict
✅ Change 'add-screen-recording' is valid
```

---

## Current Structure

```
openspec/changes/add-screen-recording/
├── proposal.md              ✅ Business case
├── design.md                ✅ Technical design
├── DESIGN_REVIEW.md         ⭐ NEW - Design review report
├── tasks.md                 ✅ Implementation tasks
└── specs/
    └── screen-recording/
        └── spec.md          ✅ Requirements deltas
```

---

## Benefits

### For Reviewers
- ✅ Clear understanding of "what changed" and "why"
- ✅ Structured analysis of issues and fixes
- ✅ Evidence-based timeline and scope justification
- ✅ Quick access to validation results

### For Implementers
- ✅ Reference for design decisions during implementation
- ✅ Understanding of trade-offs and constraints
- ✅ Mitigation strategies for known risks

### For Future Teams
- ✅ Historical context for architecture decisions
- ✅ Lessons learned from previous issues
- ✅ Reusable patterns for similar changes
- ✅ Audit trail for compliance

---

## Example: add-screen-recording

This change meets **7 out of 10 criteria** for DESIGN_REVIEW.md:

| Criteria | Met? | Evidence |
|----------|------|----------|
| 3+ Design Changes | ✅ Yes | 8 technical decisions (FFmpeg, codecs, permissions, etc.) |
| Timeline > 30% | ✅ Yes | 42% increase (12→17 weeks) |
| Scope > 50% | ✅ Yes | 100+ tasks added (50% increase) |
| HIGH/CRITICAL Risks | ✅ Yes | 5 HIGH risks identified |
| 3+ Platform Strategies | ✅ Yes | 8 platform-specific implementations |
| Compliance Issues | ✅ Yes | GPL license contamination risk |
| Refactoring | ✅ Yes | Major architecture changes |
| Knowledge Transfer | ✅ Yes | Complex cross-platform domain |
| Simple Feature | ❌ No | N/A |
| Bug Fix | ❌ No | N/A |

**Conclusion**: DESIGN_REVIEW.md was **required and appropriate** for this change.

---

## Recommendations

### For Future Changes

**Always Ask**:
1. Does this change significantly alter the original design?
2. Did we discover critical flaws that need documentation?
3. Will future teams benefit from understanding our decisions?
4. Is there compliance/security/license risk?
5. Is the timeline or scope change significant?

**If 2+ answers are YES** → Add DESIGN_REVIEW.md

### For Teams

- ✅ Use DESIGN_REVIEW.md for major refactors
- ✅ Include metrics (timeline, scope, risk)
- ✅ Provide actionable recommendations
- ✅ Archive as historical record
- ❌ Don't overuse for simple changes
- ❌ Don't duplicate content from design.md

---

## Next Steps

1. ✅ File renamed: `ANALYSIS_AND_FIXES.md` → `DESIGN_REVIEW.md`
2. ✅ Project guidelines updated in `openspec/project.md`
3. ✅ Template provided for future use
4. ✅ OpenSpec validation passed
5. 🎯 **Ready for implementation approval**

---

**Status**: All documentation standardization complete ✅
