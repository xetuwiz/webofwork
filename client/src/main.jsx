import { createRoot } from 'react-dom/client'
import './fonts.css'
import './index.css'
import App from './App.jsx'
import { ModalProvider } from './hooks/modal/useModalStore.jsx'
import { ThemeProvider } from './hooks/theme/ThemeProvider.jsx'
import store, { persistor } from './app/store.js'
import {Provider} from 'react-redux'
import './api/axiosInterceptors.js'
import { PersistGate } from 'redux-persist/integration/react'
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MotionConfig } from "framer-motion";


// PayPalScriptProvider is deliberately NOT here.
//
// It used to wrap the whole app, so every visitor -- including people who never
// open a checkout -- downloaded the PayPal SDK. It is needed by exactly one
// component (ContractOfferModal), which now provides it locally. Same for
// MotionConfig above: that one stays, because it is the mechanism that makes
// reduced motion work app-wide, and it costs nothing on its own.
const queryClient = new QueryClient();

createRoot(document.getElementById('root')).render(
    <Provider store={store}>
<PersistGate
  persistor={persistor}
  onBeforeLift={() => {
    store.dispatch({ type: "app/rehydrated" });
  }}
>    <ModalProvider>
    <QueryClientProvider client={queryClient}>
    {/* Outermost provider, above every provider that renders UI: the theme class
        has to be applied before any layout mounts, or the first paint of the app
        shell is always light. */}
    <ThemeProvider>
    {/* reducedMotion="user" makes every framer-motion animation respect the OS
        setting. It strips transform and layout animations but keeps opacity
        fades, so a user who asks for reduced motion still sees elements appear
        rather than snap in with no indication anything happened. The CSS block in
        index.css covers the Tailwind `transition-*` utilities; this covers the
        JS-driven motion. */}
    <MotionConfig reducedMotion="user">
    <App />
    </MotionConfig>
    </ThemeProvider>
  </QueryClientProvider>
    </ModalProvider>
    </PersistGate>
    </Provider>

)
