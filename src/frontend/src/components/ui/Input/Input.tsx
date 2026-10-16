import {
  forwardRef,
  useId,
  type InputHTMLAttributes,
  type ReactNode,
  type TextareaHTMLAttributes,
} from 'react'
import { LoaderCircle } from 'lucide-react'

import { classNames } from '@/lib/classNames'

interface FieldChromeProps {
  label: string
  helperText?: string
  error?: string
  startIcon?: ReactNode
  endAction?: ReactNode
  isSearching?: boolean
}

export interface InputProps
  extends
    Omit<InputHTMLAttributes<HTMLInputElement>, 'size'>,
    FieldChromeProps {}

export interface TextareaProps
  extends
    TextareaHTMLAttributes<HTMLTextAreaElement>,
    Omit<FieldChromeProps, 'startIcon' | 'isSearching'> {}

function describedBy(
  externalId: string | undefined,
  helperId: string | undefined,
  errorId: string | undefined,
) {
  return [externalId, helperId, errorId].filter(Boolean).join(' ') || undefined
}

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  {
    label,
    helperText,
    error,
    startIcon,
    endAction,
    isSearching = false,
    className,
    id: providedId,
    'aria-describedby': ariaDescribedBy,
    disabled,
    readOnly,
    ...props
  },
  ref,
) {
  const generatedId = useId()
  const id = providedId ?? generatedId
  const helperId = helperText && !error ? `${id}-helper` : undefined
  const errorId = error ? `${id}-error` : undefined

  return (
    <div
      className={classNames(
        'ui-field',
        error && 'has-error',
        disabled && 'is-disabled',
        readOnly && 'is-readonly',
      )}
    >
      <label className="ui-field__label" htmlFor={id}>
        {label}
      </label>
      <div className="ui-field__control">
        {startIcon && (
          <span className="ui-field__leading" aria-hidden="true">
            {startIcon}
          </span>
        )}
        <input
          {...props}
          ref={ref}
          id={id}
          className={classNames('ui-field__input', className)}
          disabled={disabled}
          readOnly={readOnly}
          aria-invalid={Boolean(error) || undefined}
          aria-describedby={describedBy(ariaDescribedBy, helperId, errorId)}
        />
        {isSearching && (
          <span className="ui-field__searching" aria-label="Searching">
            <LoaderCircle aria-hidden="true" size={17} />
          </span>
        )}
        {endAction && <span className="ui-field__action">{endAction}</span>}
      </div>
      {(error || helperText) && (
        <p
          className={classNames('ui-field__message', error && 'is-error')}
          id={errorId ?? helperId}
        >
          {error ?? helperText}
        </p>
      )}
    </div>
  )
})

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(
  function Textarea(
    {
      label,
      helperText,
      error,
      endAction,
      className,
      id: providedId,
      'aria-describedby': ariaDescribedBy,
      disabled,
      readOnly,
      ...props
    },
    ref,
  ) {
    const generatedId = useId()
    const id = providedId ?? generatedId
    const helperId = helperText && !error ? `${id}-helper` : undefined
    const errorId = error ? `${id}-error` : undefined

    return (
      <div
        className={classNames(
          'ui-field',
          error && 'has-error',
          disabled && 'is-disabled',
          readOnly && 'is-readonly',
        )}
      >
        <label className="ui-field__label" htmlFor={id}>
          {label}
        </label>
        <div className="ui-field__control ui-field__control--textarea">
          <textarea
            {...props}
            ref={ref}
            id={id}
            className={classNames(
              'ui-field__input ui-field__textarea',
              className,
            )}
            disabled={disabled}
            readOnly={readOnly}
            aria-invalid={Boolean(error) || undefined}
            aria-describedby={describedBy(ariaDescribedBy, helperId, errorId)}
          />
          {endAction && <span className="ui-field__action">{endAction}</span>}
        </div>
        {(error || helperText) && (
          <p
            className={classNames('ui-field__message', error && 'is-error')}
            id={errorId ?? helperId}
          >
            {error ?? helperText}
          </p>
        )}
      </div>
    )
  },
)
