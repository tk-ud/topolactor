import { assertEquals } from "https://deno.land/std/assert/mod.ts";
import {
  isValidTopologySystemName,
  topologySystemNameValidationError,
  topologySystemNameToPhysicalTable,
  topologySystemNameToRoute,
  topologySystemNameToUiBuilderKey,
  resolveVisibleTopologyName,
  TOPOLOGY_SYSTEM_NAME_PATTERN,
} from "../lib/topologySystemName.ts";

Deno.test("topologySystemName: valid names accepted", () => {
  const valid = [
    "customer-management",
    "employees",
    "order-lines",
    "a1",
    "customer-contacts",
    "abc123",
    "my-table-v2",
  ];
  for (const name of valid) {
    assertEquals(isValidTopologySystemName(name), true, `expected valid: ${name}`);
  }
});

Deno.test("topologySystemName: invalid names rejected", () => {
  const invalid = [
    "",
    "-foo",
    "foo-",
    "foo--bar",
    "Foo",
    "FOO",
    "foo bar",
    "顧客管理",
    "foo_bar",
    "-",
    "foo-",
  ];
  for (const name of invalid) {
    assertEquals(isValidTopologySystemName(name), false, `expected invalid: ${name}`);
  }
});

Deno.test("topologySystemNameToRoute: customer-management → /admin/customer-management", () => {
  assertEquals(topologySystemNameToRoute("customer-management"), "/admin/customer-management");
});

Deno.test("topologySystemNameToRoute: employees → /admin/employees", () => {
  assertEquals(topologySystemNameToRoute("employees"), "/admin/employees");
});

Deno.test("topologySystemNameToPhysicalTable: customer-management → customer_management", () => {
  assertEquals(topologySystemNameToPhysicalTable("customer-management"), "customer_management");
});

Deno.test("topologySystemNameToPhysicalTable: customer-contacts → customer_contacts", () => {
  assertEquals(topologySystemNameToPhysicalTable("customer-contacts"), "customer_contacts");
});

Deno.test("topologySystemNameToPhysicalTable: no hyphens → unchanged", () => {
  assertEquals(topologySystemNameToPhysicalTable("employees"), "employees");
});

Deno.test("topologySystemNameToUiBuilderKey: equals topology.name", () => {
  assertEquals(topologySystemNameToUiBuilderKey("customer-management"), "customer-management");
});

Deno.test("resolveVisibleTopologyName: displayName preferred over system name", () => {
  assertEquals(resolveVisibleTopologyName("顧客管理", "customer-management"), "顧客管理");
});

Deno.test("resolveVisibleTopologyName: falls back to system name when displayName empty", () => {
  assertEquals(resolveVisibleTopologyName("", "customer-management"), "customer-management");
  assertEquals(resolveVisibleTopologyName(null, "customer-management"), "customer-management");
  assertEquals(resolveVisibleTopologyName(undefined, "customer-management"), "customer-management");
});

Deno.test("resolveVisibleTopologyName: returns empty string when both missing", () => {
  assertEquals(resolveVisibleTopologyName("", ""), "");
  assertEquals(resolveVisibleTopologyName(null, null), "");
});

Deno.test("topologySystemNameValidationError: required error on empty", () => {
  const err = topologySystemNameValidationError("");
  assertEquals(typeof err, "string");
  assertEquals(err !== null, true);
});

Deno.test("topologySystemNameValidationError: null on valid name", () => {
  assertEquals(topologySystemNameValidationError("customer-management"), null);
});

Deno.test("displayName does not affect route derivation", () => {
  const sysName = "customer-management";
  const _displayName = "顧客管理";
  assertEquals(topologySystemNameToRoute(sysName), "/admin/customer-management");
  assertEquals(topologySystemNameToPhysicalTable(sysName), "customer_management");
  assertEquals(topologySystemNameToUiBuilderKey(sysName), "customer-management");
});

Deno.test("UI Builder routeKey and route navigation target_ref derive from topology.name", () => {
  const name = "customer-management";
  const uiBuilderKey = topologySystemNameToUiBuilderKey(name);
  assertEquals(uiBuilderKey, "customer-management");
  // route navigation target_ref is "route:" + uiBuilderKey
  assertEquals(`route:${uiBuilderKey}`, "route:customer-management");
  // userFacingTopologyLabel does not change the route nav ref
  const _displayName = "顧客管理";
  assertEquals(`route:${topologySystemNameToUiBuilderKey(name)}`, "route:customer-management");
});

Deno.test("userFacingTopologyLabel and screenLabel do not affect UI Builder key or route nav target", () => {
  const sysName = "customer-management";
  // Regardless of display name, UI Builder key is always topology.name
  assertEquals(topologySystemNameToUiBuilderKey(sysName), "customer-management");
  assertEquals(`route:${topologySystemNameToUiBuilderKey(sysName)}`, "route:customer-management");
  // Same holds for any Japanese display name
  const displayNames = ["顧客管理", "従業員一覧", "注文"];
  for (const _label of displayNames) {
    assertEquals(topologySystemNameToUiBuilderKey(sysName), sysName);
  }
});

Deno.test("TOPOLOGY_SYSTEM_NAME_PATTERN: pattern check", () => {
  assertEquals(TOPOLOGY_SYSTEM_NAME_PATTERN.test("customer-management"), true);
  assertEquals(TOPOLOGY_SYSTEM_NAME_PATTERN.test("-foo"), false);
  assertEquals(TOPOLOGY_SYSTEM_NAME_PATTERN.test("foo-"), false);
  assertEquals(TOPOLOGY_SYSTEM_NAME_PATTERN.test("foo--bar"), false);
});
