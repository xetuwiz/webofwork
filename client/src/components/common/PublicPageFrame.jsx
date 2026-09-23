import Header from "../../features/client/landingPage/components/Header.jsx";
import Footer from "../../features/client/landingPage/components/Footer.jsx";

export default function PublicPageFrame({ children }) {
  return <><Header /><main className="public-page">{children}</main><Footer /></>;
}