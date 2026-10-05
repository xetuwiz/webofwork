/** Minimal react-router-dom stand-in for SSR. */
import { createContext, useContext } from "react";

const RouterContext = createContext({
  params: {},
  navigate: () => {},
  location: { pathname: "/", search: "" },
  searchParams: new URLSearchParams(),
});

export function useParams() {
  return useContext(RouterContext).params;
}
export function useNavigate() {
  return useContext(RouterContext).navigate;
}
export function useLocation() {
  return useContext(RouterContext).location;
}
export function useSearchParams() {
  return [useContext(RouterContext).searchParams, () => {}];
}
export function useBlocker() {
  return { state: "unblocked", proceed() {}, reset() {} };
}
export function BrowserRouter({ children }) {
  return children;
}
export function Link({ children }) {
  return children;
}
export function NavLink({ children }) {
  return children;
}
export function Routes({ children }) {
  return children;
}
export function Route() {
  return null;
}
export function Navigate() {
  return null;
}
export function useNavigateHook() {
  return () => {};
}

export default { useParams, useNavigate };
