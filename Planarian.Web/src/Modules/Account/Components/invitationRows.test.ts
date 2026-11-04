import { InviteUserRequest } from "../Models/InviteUserRequest";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import {
  MAX_BULK_INVITATIONS,
  parseInvitationRows,
  previewInvitations,
} from "./invitationRows";

const row = (id: number, data: InviteUserRequest) => ({ id, ...data });
const person = (emailAddress: string): InviteUserRequest => ({
  firstName: "Jane",
  lastName: "Doe",
  emailAddress,
});
const existing = (
  status: "member" | "pending" | "revoked",
  email: string
): UserManagerGridVm => ({
  userId: status + "0001",
  fullName: "Jane Doe",
  emailAddress: email,
  hasActiveInvitation: status === "pending",
  accessRevokedOn: status === "revoked" ? "2026-11-01T12:00:00Z" : null,
  invitationAcceptedOn: status === "pending" ? null : "2026-09-01T12:00:00Z",
  invitationSentOn: null,
  lastActiveOn: null,
  invitationEmailAttemptCount: 0,
});

describe("bulk invitation parser", () => {
  it("pastes three spreadsheet columns with optional headings and trims cells", () => {
    expect(
      parseInvitationRows(
        "First Name\tLast Name\tEmail\n Jane \t Smith \t JANE@example.test\nJohn\tDoe\tjohn@example.test"
      )
    ).toEqual([
      {
        firstName: "Jane",
        lastName: "Smith",
        emailAddress: "JANE@example.test",
      },
      { firstName: "John", lastName: "Doe", emailAddress: "john@example.test" },
    ]);
  });

  it("recognizes reordered CSV headers and quoted delimiters", () => {
    expect(
      parseInvitationRows(
        'Email Address,Last Name,First Name\n"jane@example.test","Smith, Jr.","Jane"'
      )
    ).toEqual([
      {
        firstName: "Jane",
        lastName: "Smith, Jr.",
        emailAddress: "jane@example.test",
      },
    ]);
  });

  it("accepts headerless CSV in first, last, email order", () => {
    expect(parseInvitationRows("Jane,Smith,jane@example.test")).toEqual([
      {
        firstName: "Jane",
        lastName: "Smith",
        emailAddress: "jane@example.test",
      },
    ]);
  });

  it("rejects malformed records, incomplete headings, and over-limit batches", () => {
    expect(() => parseInvitationRows("First Name,Last Name\nJane,Doe")).toThrow(
      "header"
    );
    expect(() => parseInvitationRows("Jane,Doe")).toThrow("exactly three");
    expect(() => parseInvitationRows("\n ")).toThrow("Paste at least one");
    expect(() =>
      parseInvitationRows(
        Array(MAX_BULK_INVITATIONS + 1)
          .fill("Jane,Smith,jane@example.test")
          .join("\n")
      )
    ).toThrow("no more");
  });

  it("does not drop duplicate or invalid records silently", () => {
    const preview = previewInvitations(
      [
        row(1, person("Jane@Example.test")),
        row(2, person(" jane@example.test ")),
        row(3, {
          firstName: " ",
          lastName: "Doe",
          emailAddress: "other@example.test",
        }),
        row(4, person("not an email")),
      ],
      []
    );
    expect(preview.map((item) => item.disposition)).toEqual([
      "ready",
      "skipped",
      "invalid",
      "invalid",
    ]);
    expect(preview[1].reason).toContain("Duplicate");
  });

  it("distinguishes pending, registered, and revoked memberships", () => {
    const people = [
      existing("member", "a@example.test"),
      existing("pending", "b@example.test"),
      existing("revoked", "c@example.test"),
    ];
    const result = previewInvitations(
      [
        row(1, person("a@EXAMPLE.test")),
        row(2, person("b@example.test")),
        row(3, person("c@example.test")),
        row(4, person("new@example.test")),
      ],
      people
    );
    expect(result.map((item) => item.disposition)).toEqual([
      "skipped",
      "skipped",
      "skipped",
      "ready",
    ]);
    expect(result[0].reason).toContain("member");
    expect(result[1].reason).toContain("invited");
    expect(result[2].reason).toContain("Restore");
  });
});
