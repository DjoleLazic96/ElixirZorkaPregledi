import { BrowserRouter } from 'react-router-dom';
import { AppRoutes } from './app/AppRoutes';
import { ConfirmProvider } from './app/providers/ConfirmProvider';
import { QueryProvider } from './app/providers/QueryProvider';
import { ToastProvider } from './app/providers/ToastProvider';
import { ErrorBoundary } from './components/ErrorBoundary';
import { AuthProvider } from './features/auth/AuthProvider';

/**
 * Root komponenta — samo provideri; navigacija je u `AppRoutes`.
 *
 * Redosled (blueprint 13 §1):
 *   QueryProvider > BrowserRouter > AuthProvider > ToastProvider > ConfirmProvider > ErrorBoundary > AppRoutes
 * Auth je unutar Router-a jer `LoginPage` navigira; Toast je iznad Confirm-a da i
 * confirm handleri u stranicama mogu da pozovu `useToast()`; ErrorBoundary je
 * najniže da bi i on sam mogao da koristi providere kad padne neka ruta.
 */
export default function App() {
  return (
    <QueryProvider>
      <BrowserRouter>
        <AuthProvider>
          <ToastProvider>
            <ConfirmProvider>
              <ErrorBoundary>
                <AppRoutes />
              </ErrorBoundary>
            </ConfirmProvider>
          </ToastProvider>
        </AuthProvider>
      </BrowserRouter>
    </QueryProvider>
  );
}
