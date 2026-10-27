import { Component, type ErrorInfo, type ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Button } from './ui/Button';
import { Card, CardBody } from './ui/Card';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  greska: Error | null;
}

/**
 * Poslednja linija odbrane oko `AppRoutes` (blueprint 13 §7): umesto belog
 * ekrana korisnik vidi poruku i dugme „Osveži stranicu". Klasna komponenta jer
 * React nema hook ekvivalent za `getDerivedStateFromError`.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { greska: null };

  static getDerivedStateFromError(greska: Error): ErrorBoundaryState {
    return { greska };
  }

  componentDidCatch(greska: Error, info: ErrorInfo): void {
    // Jedino mesto gde console.error ima smisla — nema drugog kanala ka IT-u.
    console.error('Neuhvaćena greška u aplikaciji:', greska, info.componentStack);
  }

  render(): ReactNode {
    const { greska } = this.state;
    if (greska === null) return this.props.children;

    // Ako je greška stigla iz API-ja, traceId pomaže IT-u da nađe zapis u logu.
    const traceId = (greska as { response?: { data?: { traceId?: string } } }).response?.data
      ?.traceId;

    return (
      <div className="grid min-h-screen place-items-center bg-gray-50 p-4">
        <Card className="w-full max-w-md">
          <CardBody className="space-y-4 text-center">
            <div className="mx-auto flex h-10 w-10 items-center justify-center rounded-full bg-red-100 text-red-600">
              <AlertTriangle size={20} aria-hidden />
            </div>
            <div>
              <h1 className="text-base font-semibold text-gray-900">
                Došlo je do greške u aplikaciji.
              </h1>
              <p className="mt-1 text-sm text-gray-500">
                Osvežite stranicu. Ako se greška ponavlja, obavestite podršku.
              </p>
              {traceId && (
                <p className="mt-2 text-xs text-gray-400">
                  Kod greške: <code>{traceId}</code>
                </p>
              )}
            </div>
            <Button onClick={() => window.location.reload()}>Osveži stranicu</Button>
          </CardBody>
        </Card>
      </div>
    );
  }
}
